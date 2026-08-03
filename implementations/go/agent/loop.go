package agent

import (
	"context"
	"encoding/json"
	"fmt"
	"math"
	"strings"
	"time"

	"github.com/sashabaranov/go-openai"
)

type TurnResult struct {
	FinalResponse string                         `json:"final_response"`
	Messages      []openai.ChatCompletionMessage `json:"messages"`
	ApiCalls      int                            `json:"api_calls"`
	Completed     bool                           `json:"completed"`
	Failed        bool                           `json:"failed"`
	Interrupted   bool                           `json:"interrupted"`
	ExitReason    string                         `json:"exit_reason"`
	Error         string                         `json:"error,omitempty"`
}

type Agent struct {
	client             *openai.Client
	model              string
	messages           []openai.ChatCompletionMessage
	registry           *ToolRegistry
	MaxIterations      int
	MaxRetries         int
	ContextWindowLimit int
	interruptRequested bool
}

func NewAgent(apiKey string, registry *ToolRegistry, model string) *Agent {
	if model == "" {
		model = openai.GPT4TurboPreview
	}

	agent := &Agent{
		client:             openai.NewClient(apiKey),
		model:              model,
		messages:           make([]openai.ChatCompletionMessage, 0),
		registry:           registry,
		MaxIterations:      10,
		MaxRetries:         3,
		ContextWindowLimit: 30,
	}

	agent.initializeSystemPrompt()
	return agent
}

func (a *Agent) initializeSystemPrompt() {
	systemPrompt := "You are a helpful AI assistant. You have access to various tools. " +
		"When asked to perform a task, use the tools to gather information and take actions before answering."

	a.messages = append(a.messages, openai.ChatCompletionMessage{
		Role:    openai.ChatMessageRoleSystem,
		Content: systemPrompt,
	})
}

func (a *Agent) RequestInterrupt() {
	a.interruptRequested = true
}

func (a *Agent) prepareApiMessages(msgs []openai.ChatCompletionMessage) []openai.ChatCompletionMessage {
	// Phase 2.2: Shallow copy of messages slice
	apiMsgs := make([]openai.ChatCompletionMessage, len(msgs))
	copy(apiMsgs, msgs)
	return apiMsgs
}

func (a *Agent) compressContextIfNeeded(msgs []openai.ChatCompletionMessage) []openai.ChatCompletionMessage {
	// Phase 2.3: Context window protection
	if len(msgs) <= a.ContextWindowLimit {
		return msgs
	}

	fmt.Printf("  [Context Window Protection] History size (%d) > limit (%d). Trimming middle history...\n", len(msgs), a.ContextWindowLimit)

	systemPrompt := msgs[0]
	recentCount := a.ContextWindowLimit - 3
	recentMessages := msgs[len(msgs)-recentCount:]

	for len(recentMessages) > 0 && recentMessages[0].Role == openai.ChatMessageRoleTool {
		recentMessages = recentMessages[1:]
	}

	summaryMsg := openai.ChatCompletionMessage{
		Role:    openai.ChatMessageRoleSystem,
		Content: fmt.Sprintf("[System: Previous conversation history was trimmed to fit context window. %d earlier messages summarized.]", len(msgs)-len(recentMessages)-1),
	}

	result := make([]openai.ChatCompletionMessage, 0, 2+len(recentMessages))
	result = append(result, systemPrompt, summaryMsg)
	result = append(result, recentMessages...)
	return result
}

func (a *Agent) Run(ctx context.Context, userInput string) (*TurnResult, error) {
	// --- Phase 1: Turn Prologue ---
	fmt.Printf("\nUser: %s\n", userInput)

	a.messages = append(a.messages, openai.ChatCompletionMessage{
		Role:    openai.ChatMessageRoleUser,
		Content: userInput,
	})

	apiCalls := 0
	a.interruptRequested = false
	emptyContentRetries := 0

	// --- Phase 2: Main Conversation Loop ---
	for apiCalls < a.MaxIterations {
		// 2.1 Pre-API Checks
		if a.interruptRequested {
			fmt.Println("  [Turn Exit] Turn interrupted by user.")
			return &TurnResult{
				FinalResponse: "",
				Messages:      a.messages,
				ApiCalls:      apiCalls,
				Interrupted:   true,
				ExitReason:    "interrupted",
			}, nil
		}

		apiCalls++

		// 2.2 & 2.3 Message Preparation and Context Compression
		preparedMessages := a.prepareApiMessages(a.messages)
		preparedMessages = a.compressContextIfNeeded(preparedMessages)

		// 2.4 Inner Retry Loop for LLM API Call
		var resp openai.ChatCompletionResponse
		var err error

		for retry := 0; retry < a.MaxRetries; retry++ {
			req := openai.ChatCompletionRequest{
				Model:    a.model,
				Messages: preparedMessages,
			}

			tools := a.registry.GetToolSchemas()
			if len(tools) > 0 {
				req.Tools = tools
			}

			resp, err = a.client.CreateChatCompletion(ctx, req)
			if err == nil {
				break
			}

			fmt.Printf("  [API Error Retry %d/%d] %v\n", retry+1, a.MaxRetries, err)
			if retry == a.MaxRetries-1 {
				return &TurnResult{
					FinalResponse: "",
					Messages:      a.messages,
					ApiCalls:      apiCalls,
					Failed:        true,
					ExitReason:    "api_error",
					Error:         err.Error(),
				}, nil
			}

			backoffSec := time.Duration(math.Pow(2, float64(retry))) * time.Second
			time.Sleep(backoffSec)
		}

		if len(resp.Choices) == 0 {
			return &TurnResult{
				FinalResponse: "",
				Messages:      a.messages,
				ApiCalls:      apiCalls,
				Failed:        true,
				ExitReason:    "no_response",
				Error:         "No choices in completion response.",
			}, nil
		}

		choice := resp.Choices[0]
		msg := choice.Message

		a.messages = append(a.messages, msg)

		// 2.6 Tool Call Execution Path
		if len(msg.ToolCalls) > 0 {
			schemas := a.registry.GetToolSchemas()
			registeredNames := make(map[string]bool)
			for _, s := range schemas {
				registeredNames[s.Function.Name] = true
			}

			for _, toolCall := range msg.ToolCalls {
				name := toolCall.Function.Name
				argsStr := toolCall.Function.Arguments

				// Self-correction for unregistered tools
				if !registeredNames[name] {
					avail := make([]string, 0, len(registeredNames))
					for k := range registeredNames {
						avail = append(avail, k)
					}
					errorMsg := fmt.Sprintf("Error: Tool '%s' is not registered. Available tools: %s", name, strings.Join(avail, ", "))
					fmt.Printf("  [Tool Validation Error] %s\n", errorMsg)

					a.messages = append(a.messages, openai.ChatCompletionMessage{
						Role:       openai.ChatMessageRoleTool,
						Content:    errorMsg,
						Name:       name,
						ToolCallID: toolCall.ID,
					})
					continue
				}

				// Validate JSON arguments
				var dummy map[string]interface{}
				if argsStr != "" {
					if jsonErr := json.Unmarshal([]byte(argsStr), &dummy); jsonErr != nil {
						errorMsg := fmt.Sprintf("Error: Invalid JSON arguments for tool '%s': %v", name, jsonErr)
						fmt.Printf("  [JSON Parse Error] %s\n", errorMsg)

						a.messages = append(a.messages, openai.ChatCompletionMessage{
							Role:       openai.ChatMessageRoleTool,
							Content:    errorMsg,
							Name:       name,
							ToolCallID: toolCall.ID,
						})
						continue
					}
				}

				fmt.Printf("  [Tool Execution] %s(%s)\n", name, argsStr)

				// Execute tool
				result := a.registry.ExecuteTool(name, argsStr)
				fmt.Printf("  [Tool Result] %s\n", result)

				a.messages = append(a.messages, openai.ChatCompletionMessage{
					Role:       openai.ChatMessageRoleTool,
					Content:    result,
					Name:       name,
					ToolCallID: toolCall.ID,
				})
			}
			// Continue loop to send tool results back to LLM
			continue
		}

		// 2.7 Final Text Response Path
		finalText := strings.TrimSpace(msg.Content)

		// Empty Response Recovery
		if finalText == "" {
			if emptyContentRetries < 2 {
				emptyContentRetries++
				fmt.Println("  [Empty Response Recovery] Retrying with prompt nudge...")
				a.messages = append(a.messages, openai.ChatCompletionMessage{
					Role:    openai.ChatMessageRoleUser,
					Content: "Please provide a complete text response summarizing your answer.",
				})
				continue
			} else {
				finalText = "(empty response)"
			}
		}

		fmt.Printf("Assistant: %s\n", finalText)

		// --- Phase 4: Turn Finalization ---
		return &TurnResult{
			FinalResponse: finalText,
			Messages:      a.messages,
			ApiCalls:      apiCalls,
			Completed:     true,
			ExitReason:    "text_response",
		}, nil
	}

	// Exceeded iteration budget
	fmt.Printf("  [Turn Exit] Reached max iterations (%d).\n", a.MaxIterations)
	return &TurnResult{
		FinalResponse: "Reached maximum iteration limit.",
		Messages:      a.messages,
		ApiCalls:      apiCalls,
		Failed:        true,
		ExitReason:    "budget_exhausted",
	}, nil
}
