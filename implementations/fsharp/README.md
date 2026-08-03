# F# Agent Implementation

> **Mapping to Abstract Workflow:** [CONVERSATION_LOOP_WORKFLOW.md](../../docs/CONVERSATION_LOOP_WORKFLOW.md)

## Overview

The F# implementation maximizes **functional programming paradigms** to create a highly composable, flexible, and pure architecture for the 4-phase Agent Conversation Loop workflow.

## Key Functional Architecture Highlights

1. **Pure Immutable State Pipeline (`TurnState`)**
   - The entire turn state (`Messages`, `ApiCalls`, `EmptyContentRetries`, `InterruptRequested`, `Config`) is passed as an immutable record. No `mutable` variables or flags are modified during turn loop execution.
2. **Tail-Recursive Async Loop (`AgentPipeline.runTurnLoop`)**
   - Implements the main loop as a pure, tail-recursive async function, eliminating procedural `while` loops and mutable state mutations.
3. **Step Pipeline Composition (`|>`)**
   - Message payload preparation and context compression are composed using F#'s forward piping operator:
     ```fsharp
     let preparedPayload = 
         state.Messages 
         |> prepareApiMessages 
         |> compressContextIfNeeded state.Config.ContextWindowLimit
     ```
4. **Monadic Step Result Control Flow (`StepResult<'State, 'Result>`)**
   - Clean discriminated union (`Continue 'State | Exit 'Result`) for sequencing pre-checks and exit conditions.
5. **First-Class Function Composition & Partial Application (`LlmCaller` & `ToolExecutor`)**
   - The LLM caller and tool execution logic are defined as first-class, composable function types:
     ```fsharp
     type LlmCaller = FunctionDefinition list -> ChatRequestMessage list -> Async<Result<ChatCompletions, string>>
     type ToolExecutor = string -> string -> Async<string>
     ```
   - Enables partial application, composable middleware (logging, rate limits, metrics), and dependency-injected unit testing without mock frameworks.

## File Structure

- `Types.fs`: Domain types (`ExitReason`, `TurnResult`, `TurnState`, `StepResult`, `LlmCaller`, `ToolExecutor`).
- `ToolRegistry.fs`: Functional tool registry exposing composable `AsExecutor: ToolExecutor`.
- `Agent.fs`: `AgentPipeline` module containing pure functional step functions and tail-recursive loop, wrapped by an `Agent` class interface.
- `Program.fs`: Console CLI entry point with registered mock F# tools.
- `AgentPlatformFSharp.fsproj`: F# project file targetting .NET 8.0.

## Workflow Mapping

### 1. Phase 1: Turn Prologue
- Executed in `agent.RunAsync(userInput: string) : Async<TurnResult>`.
- Appends `ChatRequestUserMessage` to canonical messages.
- Constructs initial immutable `TurnState`.

### 2. Phase 2: Main Conversation Loop
- **2.1 Pre-API Checks:** Composable step functions `checkInterrupt` and `checkBudget` returning `StepResult`.
- **2.2 & 2.3 Message Prep & Context Window Protection:** Composed via forward pipeline operator (`|>`).
- **2.4 Inner Retry Loop:** Implemented using recursive F# async function `callLlmWithRetry`.
- **2.5 Response Classification:** Pattern matched using `match apiResult with | Ok completions -> ... | Error err -> ...`.
- **2.6 Tool Execution Path:** `processToolCalls` asynchronously executes tool calls in parallel using `Async.Parallel` and returns updated immutable state.
- **2.7 Final Text Response Path:** `processTextResponse` pattern matches empty text content with prompt nudge retries.

### 3. Phase 3 & 4: Turn Finalization
- Returns immutable F# `TurnResult` record.
