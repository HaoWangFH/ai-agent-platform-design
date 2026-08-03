# C# Agent Implementation

> **Mapping to Abstract Workflow:** [CONVERSATION_LOOP_WORKFLOW.md](../../docs/CONVERSATION_LOOP_WORKFLOW.md)

## Overview

The C# implementation models the 4-phase Agent Conversation Loop workflow using .NET 8, `Azure.AI.OpenAI` SDK, and strongly typed `TurnResult` objects under the root namespace **`Skight.AgentPlatform`**.

## Project Architecture & File Structure

```
implementations/csharp/
├── Skight.AgentPlatform.sln
├── src/
│   └── Skight.AgentPlatform/            (Core Executable App)
│       ├── Skight.AgentPlatform.csproj
│       ├── Agent.cs                      (4-phase conversation loop & state machine)
│       ├── ToolRegistry.cs               (Tool registration & execution runtime)
│       ├── Tools.cs                      (Mock tool definitions)
│       └── Program.cs                    (CLI entry point with .env & Entra ID support)
└── tests/
    ├── Skight.AgentPlatform.Tests/      (xUnit + FluentAssertions Unit & Spec Tests)
    ├── Skight.AgentPlatform.MSpec.Tests/  (Machine.Specifications BDD Context-Spec Tests)
    └── Skight.AgentPlatform.LightBDD.Tests/ (LightBDD Code-First Scenario BDD Tests)
```

## Running Tests

Run all 3 specification test suites via the solution file:

```powershell
dotnet test implementations/csharp/Skight.AgentPlatform.sln
```

Or run individual test frameworks:
- **xUnit**: `dotnet test implementations/csharp/tests/Skight.AgentPlatform.Tests`
- **MSpec**: `dotnet test implementations/csharp/tests/Skight.AgentPlatform.MSpec.Tests`
- **LightBDD**: `dotnet test implementations/csharp/tests/Skight.AgentPlatform.LightBDD.Tests`

## Workflow Mapping

### 1. Phase 1: Turn Prologue
- Executed in `Agent.RunAsync(string userInput): Task<TurnResult>`.
- Appends `ChatRequestUserMessage` to `_messages`.
- Resets per-turn state: `apiCalls = 0`, `_interruptRequested = false`, `emptyContentRetries = 0`.

### 2. Phase 2: Main Conversation Loop
- **2.1 Pre-API Checks:** Checked at `while (apiCalls < MaxIterations)`. Checks `_interruptRequested` and `MaxIterations`.
- **2.2 Message Preparation:** `PrepareApiMessages()` shallow-copies `_messages`.
- **2.3 Context Window Protection:** `CompressContextIfNeeded()` trims middle history when message count > `ContextWindowLimit`.
- **2.4 Inner Retry Loop:** Retry loop with `await Task.Delay((int)Math.Pow(2, retry) * 1000)`.
- **2.5 Response Normalization:** Accesses `completions.Choices[0].Message`.
- **2.6 Tool Execution Path:**
  - Validates tool schema against registered tool names (unregistered tool self-correction).
  - Validates JSON using `JsonDocument.Parse`.
  - Executes tool with `try...catch` runtime error handling.
  - Inserts assistant message with `ToolCalls` before tool response messages and continues loop.
- **2.7 Final Text Response Path:**
  - Retries empty text responses with prompt nudges before returning fallback text `"(empty response)"`.
  - Returns `TurnResult` with `Completed = true, ExitReason = "text_response"`.

### 3. Phase 3 & 4: Turn Finalization
- Returns structured `TurnResult` object.
