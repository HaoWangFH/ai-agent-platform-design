# AI Agent Platform Design

This repository contains the architectural analysis, abstract conversation loop specification, and parallel implementations of a lightweight AI Agent platform. The goal is to compare different programming languages and their paradigms when building the core Agent Loop.

## Structure
- `docs/CONVERSATION_LOOP_WORKFLOW.md`: The abstract 4-phase Agent Conversation Loop workflow specification (Hermes Agent architecture).
- `architecture-analysis.zh.md`: The original architecture analysis document comparing Hermes Agent and Claude Code.
- `spec/`: Language-agnostic specifications for the abstract agent loop, tool definitions, and design mapping.
- `implementations/`: The parallel reference implementations of the core agent loop in different languages:
  - `python/` (OOP & dataclasses)
  - `typescript/` (Async/await & Node.js)
  - `csharp/` (Azure OpenAI & .NET 8)
  - `go/` (Goroutines, channels & Go idiomatic structs)
  - `fsharp/` (Discriminated unions, record types & forward piping `|>`)

## Rationale
By abstracting the core logic and implementing it in multiple languages, we can evaluate the ecosystem, concurrency primitives, and type safety of each language side-by-side. This helps in making informed technical decisions for building a production-grade multi-agent platform.
