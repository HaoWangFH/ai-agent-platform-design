# AI Agent Platform Design

This repository contains the architectural analysis, abstract conversation loop specification, and parallel implementations of a lightweight AI Agent platform. The goal is to compare different programming languages and their paradigms when building the core Agent Loop.

## Structure / 目录结构
- `docs/CONVERSATION_LOOP_WORKFLOW.md` ([中文版 docs/CONVERSATION_LOOP_WORKFLOW.zh.md](docs/CONVERSATION_LOOP_WORKFLOW.zh.md)): The abstract 4-phase Agent Conversation Loop workflow specification (Hermes Agent architecture).
- `architecture-analysis.zh.md`: The original architecture analysis document comparing Hermes Agent and Claude Code.
- `spec/`: Language-agnostic specifications for the abstract agent loop, tool definitions, and design mapping.
- `implementations/`: The parallel reference implementations of the core agent loop in different languages:
  - `python/` ([README.md](implementations/python/README.md) | [中文 README.zh.md](implementations/python/README.zh.md))
  - `typescript/` ([README.md](implementations/typescript/README.md) | [中文 README.zh.md](implementations/typescript/README.zh.md))
  - `csharp/` ([README.md](implementations/csharp/README.md) | [中文 README.zh.md](implementations/csharp/README.zh.md))
  - `go/` ([README.md](implementations/go/README.md) | [中文 README.zh.md](implementations/go/README.zh.md))
  - `fsharp/` ([README.md](implementations/fsharp/README.md) | [中文 README.zh.md](implementations/fsharp/README.zh.md))

## Rationale
By abstracting the core logic and implementing it in multiple languages, we can evaluate the ecosystem, concurrency primitives, and type safety of each language side-by-side. This helps in making informed technical decisions for building a production-grade multi-agent platform.
