# F# Agent Implementation

> **Mapping to Abstract Workflow:** [CONVERSATION_LOOP_WORKFLOW.md](../../docs/CONVERSATION_LOOP_WORKFLOW.md)

## Overview

The F# implementation models the 4-phase Agent Conversation Loop workflow using F#'s functional programming paradigms: **discriminated unions**, **record types**, **pattern matching**, **async workflows**, and **forward pipeline operators (`|>`)**.

## File Structure

- `Types.fs`: Algebraic data types (`ExitReason` discriminated union, `TurnResult` record type, `AgentConfig` record type).
- `ToolRegistry.fs`: Functional tool registry supporting F# `Async<string>` tool handlers.
- `Agent.fs`: Functional Agent workflow implementation leveraging forward piping and recursive retries.
- `Program.fs`: Console CLI entry point with registered mock F# tools.
- `AgentPlatformFSharp.fsproj`: F# project file targetting .NET 8.0.

## Workflow Mapping

### 1. Phase 1: Turn Prologue
- Executed in `agent.RunAsync(userInput: string) : Async<TurnResult>`.
- Appends `ChatRequestUserMessage` to `messages`.
- Resets per-turn state (`apiCalls`, `interruptRequested`, `emptyContentRetries`).

### 2. Phase 2: Main Conversation Loop
- **2.1 Pre-API Checks:** Checked at start of `while apiCalls < config.MaxIterations && turnResult.IsNone do`. Checks `interruptRequested` and `config.MaxIterations`.
- **2.2 & 2.3 Message Prep & Context Window Protection:** Uses forward pipeline operator (`|>`) for clean data transformation:
  ```fsharp
  let preparedMessages = 
      messages 
      |> self.PrepareApiMessages 
      |> self.CompressContextIfNeeded
  ```
- **2.4 Inner Retry Loop:** Implemented using recursive F# async function `ExecuteApiWithRetry(messages, retryCount)` with `do! Async.Sleep delayMs`.
- **2.5 Response Classification:** Pattern matched using `match apiResult with | Ok completions -> ... | Error err -> ...`.
- **2.6 Tool Execution Path:**
  - Evaluates registered tool names set (`registeredNames.Contains(name)`).
  - Handles JSON parsing safely within F# `try...with` block.
  - Asynchronously executes tool handler and constructs tool result message.
  - Appends tool messages and continues `while` loop.
- **2.7 Final Text Response Path:**
  - Pattern matches empty text content with prompt nudge retries before setting fallback text `"(empty response)"`.
  - Returns `TurnResult` with `Completed = true, ExitReason = TextResponse finalText`.

### 3. Phase 3 & 4: Turn Finalization
- Returns immutable F# `TurnResult` record type.
