# AI Agent Platform Design

This repository contains the architectural analysis, abstract conversation loop specification, parity progress tracking, and parallel implementations of a lightweight, enterprise-ready AI Agent platform (`Skight.AgentPlatform` in C# and `Skight.AgentPlatform.FSharp` in F#).

## 🔄 SDLC Lifecycle & Repository Directory Sitemap / SDLC 目录结构

This repository follows a strict 7-phase **Software Development Life Cycle (SDLC)** with sequential directory numbering:

| SDLC Phase | Development Stage | Directory | Description & Key Artifacts |
| :--- | :--- | :--- | :--- |
| **Phase 1** | **Analyze** | [`03-Analysis/`](03-Analysis/) | Benchmark framework gap analysis (`HERMES_CLAUDE_CODE_ANALYSIS.md`), parity report (`HERMES_PARITY_REPORT.md`), boundary logic research (`HERMES_LOOP_CONDITIONS.md`). |
| **Phase 2** | **Design** | [`04-Architecture/`](04-Architecture/) | System architecture, 5-layer conversation loop (`ITERATION_LOOP_DESIGN.md`), multi-turn workflows (`CONVERSATION_LOOP_WORKFLOW.md`). |
| **Phase 3** | **Specification** | [`05-Specification-Driven-Development/`](05-Specification-Driven-Development/) | BDD specifications (`GAME_CHANGER_FEATURES_SPECS.md`, `AGENT_LOOP_BDD_SPECS.md`). |
| **Phase 4** | **Planning** | [`06-Tasks/`](06-Tasks/) | Master task backlog & implementation progress ([GAME_CHANGER_FEATURES_TASKS.md](06-Tasks/GAME_CHANGER_FEATURES_TASKS.md)). |
| **Phase 5** | **Implement** | [`implementations/`](implementations/) | C# core library & WebAPI/gRPC server wrapper (`csharp/`), F# core library & Expecto specs (`fsharp/`). |
| **Phase 6** | **Verify** | [`07-Testing/`](07-Testing/) | Automated test suites (Expecto 60/60, xUnit 20/20) and quality stop gates (`pre_verify`). |
| **Phase 7** | **Knowledge** | [`08-Knowledge-System/`](08-Knowledge-System/) | Domain terminology dictionary & post-implementation operational knowledge ([KNOWLEDGE_INDEX.md](08-Knowledge-System/KNOWLEDGE_INDEX.md)). |

### 🛠️ Supporting Engineering Directories
- **`00-Getting-Started/`**: Onboarding & setup guidelines.
- **`01-Engineering-Workflow/`**: Full-lifecycle engineering methodology guidelines.
- **`02-AI-Workspace/`**: AI assistant workspace configuration & guidelines.
- **`09-Standards/`**: Coding standards, F# porting guidelines, and commit conventions.
- **`10-Skill-Library/` & `11-Prompt-Library/`**: Reusable agent skills and system prompts.
- **`12-Templates/` & `13-Automation/`**: Project boilerplates and CI/CD automation.
- **`14-Agent-Framework/`**: Extracted agent interfaces & infrastructure specs.
- **`15-Cloud-Infrastructure/`**: Vendor-neutral cloud infrastructure, Kubernetes, Docker, & deployment configs.

## 🎯 Rationale
By abstracting core agent logic and implementing it in C# (.NET 10) and F#, we provide a robust foundation for multi-tenant, cloud-native enterprise AI agent server deployments with strict BDD specification verification.
