# AI Agent Platform Design

This repository contains the architectural analysis and parallel implementations of a lightweight AI Agent platform. The goal is to compare different programming languages and their paradigms when building the core Agent Loop.

## Structure
- `architecture-analysis.zh.md`: The original architecture analysis document comparing Hermes Agent and Claude Code.
- `spec/`: The language-agnostic specification for the abstract agent loop and tool definitions.
- `implementations/`: The parallel reference implementations of the core agent loop in different languages:
  - `python/`
  - `typescript/`
  - `csharp/`
  - `go/`

## Rationale
By abstracting the core logic and implementing it in multiple languages, we can evaluate the ecosystem, concurrency primitives, and type safety of each language side-by-side. This helps in making informed technical decisions for building a production-grade multi-agent platform.
