# AI Agent Platform Design

This repository contains the architectural analysis, abstract conversation loop specification, parity progress tracking, and parallel implementations of a lightweight, enterprise-ready AI Agent platform (`Skight.AgentPlatform` in C# and `Skight.AgentPlatform.FSharp` in F#).

## 🔄 SDLC Lifecycle & Repository Sitemap / SDLC 目录结构

This repository follows a strict 7-phase **Software Development Life Cycle (SDLC)**:

| SDLC Phase | Stage | Directory | Description & Key Artifacts |
| :--- | :--- | :--- | :--- |
| **Phase 1** | **Analyze** | [`03-Analysis/`](03-Analysis/) | Benchmark framework gap analysis (`HERMES_CLAUDE_CODE_ANALYSIS.md`), parity report (`HERMES_PARITY_REPORT.md`), boundary logic research (`HERMES_LOOP_CONDITIONS.md`). |
| **Phase 2** | **Design** | [`07-Architecture/`](07-Architecture/) | System architecture, 5-layer conversation loop (`ITERATION_LOOP_DESIGN.md`), multi-turn workflows (`CONVERSATION_LOOP_WORKFLOW.md`). |
| **Phase 3** | **Specification** | [`08-Specification-Driven-Development/`](08-Specification-Driven-Development/) | BDD specifications (`GAME_CHANGER_FEATURES_SPECS.md`, `AGENT_LOOP_BDD_SPECS.md`). |
| **Phase 4** | **Planning** | [`14-Tasks/`](14-Tasks/) | Master task backlog & implementation progress ([GAME_CHANGER_FEATURES_TASKS.md](14-Tasks/GAME_CHANGER_FEATURES_TASKS.md)). |
| **Phase 5** | **Implement** | [`implementations/`](implementations/) | C# core library & WebAPI/gRPC server wrapper (`csharp/`), F# core library & Expecto specs (`fsharp/`). |
| **Phase 6** | **Verify** | [`09-Testing/`](09-Testing/) | Automated test suites (Expecto 60/60, xUnit 20/20) and quality stop gates (`pre_verify`). |
| **Phase 7** | **Knowledge** | [`03-Knowledge-System/`](03-Knowledge-System/) | Domain terminology dictionary & post-implementation operational knowledge ([KNOWLEDGE_INDEX.md](03-Knowledge-System/KNOWLEDGE_INDEX.md)). |

## 🎯 Rationale
By abstracting core agent logic and implementing it in C# (.NET 10) and F#, we provide a robust foundation for multi-tenant, cloud-native enterprise AI agent server deployments with strict BDD specification verification.
