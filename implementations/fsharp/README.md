# F# Agent Implementation

> **Mapping to Abstract Workflow:** [CONVERSATION_LOOP_WORKFLOW.md](../../docs/CONVERSATION_LOOP_WORKFLOW.md)

## Overview

The F# implementation models the 4-phase Agent Conversation Loop workflow using pure functional programming, immutable record state transitions, tail-recursive async loops, and Expecto specification testing under the root namespace **`Skight.AgentPlatform.FSharp`**.

## Project Architecture & File Structure

```
implementations/fsharp/
├── Skight.AgentPlatform.FSharp.sln
├── src/
│   └── Skight.AgentPlatform.FSharp/           (Core Executable Library & App)
│       ├── Skight.AgentPlatform.FSharp.fsproj
│       ├── Types.fs                             (Domain types & discriminated unions)
│       ├── ToolRegistry.fs                      (Immutable & thread-safe tool registry)
│       ├── Agent.fs                             (Pure tail-recursive agent loop pipeline)
│       └── Program.fs                           (CLI entry point with .env & Entra ID support)
└── tests/
    └── Skight.AgentPlatform.FSharp.Tests/     (Expecto Functional Specification Tests)
        ├── Skight.AgentPlatform.FSharp.Tests.fsproj
        ├── AgentPipelineTests.fs                (Pure pipeline step specifications)
        ├── SequentialToolWorkflowSpec.fs        (Multi-turn sequential tool call specification)
        └── Main.fs                              (Expecto CLI test runner entry point)
```

## Running Tests (Expecto Framework)

Run the Expecto test suite via the solution file:

```powershell
dotnet test implementations/fsharp/Skight.AgentPlatform.FSharp.sln
```

Or run Expecto directly with detailed CLI flags:

```powershell
dotnet run --project implementations/fsharp/tests/Skight.AgentPlatform.FSharp.Tests
```

## Functional Design Highlights

- **Pure State Transitions**: `TurnState` is immutable. Pipeline steps return `StepResult<TurnState, TurnResult>`.
- **Tail-Recursive Async Loop**: `runTurnLoop` uses F# tail-recursion (`return! runTurnLoop ...`) to execute arbitrary tool iterations without stack growth.
- **Expecto BDD Specs**: Tests are declared as first-class values using Expecto `testList` and `testAsync` trees.
