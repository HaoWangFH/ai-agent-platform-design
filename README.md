# AI Agent Platform Design

This repository contains the architectural analysis, abstract conversation loop specification, parity progress tracking, and parallel implementations of a lightweight, enterprise-ready AI Agent platform (`Skight.AgentPlatform` in C# and `Skight.AgentPlatform.FSharp` in F#).

## 📁 Repository Sitemap / 目录结构

- **`03-Knowledge-System/`**: Knowledge Base, domain concept definitions, and operational terminology ([KNOWLEDGE_INDEX.md](03-Knowledge-System/KNOWLEDGE_INDEX.md) | [中文版](03-Knowledge-System/KNOWLEDGE_INDEX.zh.md)).
- **`07-Architecture/`**: Comprehensive architecture designs, comparative analysis, and loop specifications:
  - [CONVERSATION_LOOP_WORKFLOW.md](07-Architecture/CONVERSATION_LOOP_WORKFLOW.md) ([中文版](07-Architecture/CONVERSATION_LOOP_WORKFLOW.zh.md)): Abstract 4-phase Agent Conversation Loop.
  - [HERMES_LOOP_CONDITIONS.md](07-Architecture/HERMES_LOOP_CONDITIONS.md) ([中文版](07-Architecture/HERMES_LOOP_CONDITIONS.zh.md)): Analysis of all 37 loop continue/exit conditions.
  - [HERMES_PARITY_REPORT.md](07-Architecture/HERMES_PARITY_REPORT.md) ([中文版](07-Architecture/HERMES_PARITY_REPORT.zh.md)): Feature parity report.
  - [MAJOR_GAME_CHANGER_FEATURES_ARCH.md](07-Architecture/MAJOR_GAME_CHANGER_FEATURES_ARCH.md) ([中文版](07-Architecture/MAJOR_GAME_CHANGER_FEATURES_ARCH.zh.md)): Game-changer feature architecture.
- **`08-Specification-Driven-Development/`**: BDD specifications for agent loop and features ([GAME_CHANGER_FEATURES_SPECS.md](08-Specification-Driven-Development/GAME_CHANGER_FEATURES_SPECS.md)).
- **`14-Tasks/`**: Master task backlog and progress tracking ([GAME_CHANGER_FEATURES_TASKS.md](14-Tasks/GAME_CHANGER_FEATURES_TASKS.md) | [中文版](14-Tasks/GAME_CHANGER_FEATURES_TASKS.zh.md)).
- **`implementations/`**: Parallel reference implementations:
  - `csharp/` — `Skight.AgentPlatform.sln` (C# core library, xUnit, ASP.NET Core & gRPC server wrapper)
  - `fsharp/` — `Skight.AgentPlatform.FSharp.sln` (F# core library & Expecto spec suite)

## 🎯 Rationale
By abstracting core agent logic and implementing it in C# (.NET 10) and F#, we provide a robust foundation for multi-tenant, cloud-native enterprise AI agent server deployments with strict BDD specification verification.
