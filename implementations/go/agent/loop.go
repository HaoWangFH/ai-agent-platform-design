package agent

import (
	"context"
	"fmt"

	"github.com/sashabaranov/go-openai"
)

type Agent struct {
	client   *openai.Client
	model    string
	messages []openai.ChatCompletionMessage
	registry *ToolRegistry
}

func NewAgent(apiKey string, registry *ToolRegistry, model string) *Agent {
	if model == "" {
		model = openai.GPT4TurboPreview
	}
	
	agent := &Agent{
		client:   openai.NewClient(apiKey),
		model:    model,
		messages: make([]openai.ChatCompletionMessage, 0),
		registry: registry,
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

func (a *Agent) Run(ctx context.Context, userInput string) (string, error) {
	fmt.Printf("\nUser: %s\n", userInput)
	
	a.messages = append(a.messages, openai.ChatCompletionMessage{
		Role:    openai.ChatMessageRoleUser,
		Content: userInput,
	})

	for {
		req := openai.ChatCompletionRequest{
			Model:    a.model,
			Messages: a.messages,
		}

		tools := a.registry.GetToolSchemas()
		if len(tools) > 0 {
			req.Tools = tools
		}

		resp, err := a.client.CreateChatCompletion(ctx, req)
		if err != nil {
			return "", fmt.Errorf("ChatCompletion error: %v", err)
		}

		choice := resp.Choices[0]
		msg := choice.Message

		a.messages = append(a.messages, msg)

		if len(msg.ToolCalls) > 0 {
			for _, toolCall := range msg.ToolCalls {
				name := toolCall.Function.Name
				args := toolCall.Function.Arguments
				
				fmt.Printf("  [Tool Execution] %s(%s)\n", name, args)
				
				result := a.registry.ExecuteTool(name, args)
				fmt.Printf("  [Tool Result] %s\n", result)
				
				a.messages = append(a.messages, openai.ChatCompletionMessage{
					Role:       openai.ChatMessageRoleTool,
					Content:    result,
					Name:       name,
					ToolCallID: toolCall.ID,
				})
			}
			// Continue loop to send tool results to LLM
			continue
		}

		fmt.Printf("Assistant: %s\n", msg.Content)
		return msg.Content, nil
	}
}
