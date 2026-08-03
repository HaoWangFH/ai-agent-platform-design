# Go Agent Implementation

> **Mapping to Abstract Workflow:** [CONVERSATION_LOOP_WORKFLOW.md](../../docs/CONVERSATION_LOOP_WORKFLOW.md)

## Overview

The Go implementation models the 4-phase Agent Conversation Loop workflow using Go idiomatic structs, `context.Context` cancellation/timeouts, and `github.com/sashabaranov/go-openai`.

## File Structure

- `agent/loop.go`: `Agent` struct, `TurnResult` struct, and 4-phase loop execution.
- `agent/registry.go`: `ToolRegistry` managing Go function handlers and parameters JSON.
- `main.go`: Interactive CLI entry point.

## Workflow Mapping

### 1. Phase 1: Turn Prologue
- Executed in `(a *Agent) Run(ctx context.Context, userInput string) (*TurnResult, error)`.
- Appends `openai.ChatMessageRoleUser` to `a.messages`.
- Resets per-turn state: `apiCalls = 0`, `a.interruptRequested = false`, `emptyContentRetries = 0`.

### 2. Phase 2: Main Conversation Loop
- **2.1 Pre-API Checks:** Checked at start of `for apiCalls < a.MaxIterations`. Checks `a.interruptRequested` and `MaxIterations`.
- **2.2 Message Preparation:** `prepareApiMessages()` creates a shallow copy slice of `a.messages`.
- **2.3 Context Window Protection:** `compressContextIfNeeded()` trims middle messages when `len(msgs) > a.ContextWindowLimit`.
- **2.4 Inner Retry Loop:** Retry loop with exponential `time.Sleep` on API errors.
- **2.5 Response Normalization:** Accesses `resp.Choices[0].Message`.
- **2.6 Tool Execution Path:**
  - Checks if tool is registered (unregistered tool self-correction).
  - Validates JSON arguments with `json.Unmarshal`.
  - Executes tool handler and captures error strings into `openai.ChatMessageRoleTool` content.
  - Continues loop to send tool results back to LLM.
- **2.7 Final Text Response Path:**
  - Handles empty text responses with prompt nudges before returning fallback text `"(empty response)"`.
  - Returns `TurnResult` with `Completed: true, ExitReason: "text_response"`.

### 3. Phase 3 & 4: Turn Finalization
- Returns structured `*TurnResult` pointer.
