package agent

import (
	"context"
	"fmt"
	"strings"
	"testing"

	"github.com/sashabaranov/go-openai"
)

func TestToolRegistry_ExecuteTool_Success(t *testing.T) {
	registry := NewToolRegistry()
	registry.Register("echo", "Echo tool", func(args map[string]interface{}) (string, error) {
		msg, _ := args["msg"].(string)
		return fmt.Sprintf("Echo: %s", msg), nil
	}, nil)

	result := registry.ExecuteTool("echo", `{"msg":"hello"}`)
	if result != "Echo: hello" {
		t.Errorf("Expected 'Echo: hello', got '%s'", result)
	}
}

func TestToolRegistry_ExecuteTool_Unregistered(t *testing.T) {
	registry := NewToolRegistry()
	result := registry.ExecuteTool("unknown", `{}`)
	if !strings.Contains(result, "Error: Tool 'unknown' not found.") {
		t.Errorf("Expected error string for unregistered tool, got '%s'", result)
	}
}

func TestAgent_InterruptGuard(t *testing.T) {
	registry := NewToolRegistry()
	ag := NewAgent("dummy_key", registry, "gpt-4-turbo")
	ag.RequestInterrupt()

	ctx := context.Background()
	res, err := ag.Run(ctx, "Hello")
	if err != nil {
		t.Fatalf("Unexpected error: %v", err)
	}

	if !res.Interrupted {
		t.Errorf("Expected Interrupted to be true")
	}
	if res.ExitReason != "interrupted" {
		t.Errorf("Expected ExitReason 'interrupted', got '%s'", res.ExitReason)
	}
}

func TestAgent_ContextWindowProtection(t *testing.T) {
	registry := NewToolRegistry()
	ag := NewAgent("dummy_key", registry, "gpt-4-turbo")
	ag.ContextWindowLimit = 10

	msgs := make([]openai.ChatCompletionMessage, 0)
	msgs = append(msgs, openai.ChatCompletionMessage{
		Role:    openai.ChatMessageRoleSystem,
		Content: "sys",
	})
	for i := 0; i < 15; i++ {
		msgs = append(msgs, openai.ChatCompletionMessage{
			Role:    openai.ChatMessageRoleUser,
			Content: fmt.Sprintf("msg %d", i),
		})
	}

	compressed := ag.CompressContextIfNeeded(msgs)
	if len(compressed) > 10 {
		t.Errorf("Expected compressed history length <= 10, got %d", len(compressed))
	}
	if compressed[0].Content != "sys" {
		t.Errorf("Expected system prompt at index 0")
	}
}
