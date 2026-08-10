# AI Agent Platform Engineering Workflow & SDLC Lifecycle Guide

This document outlines how the AI Agent Platform (`Skight.AgentPlatform`) structures and tracks every phase of the **Software Development Life Cycle (SDLC)**.

---

## 🔄 SDLC Lifecycle Phase Mapping

| Phase | SDLC Stage | Dedicated Directory | Primary Responsibilities & Artifacts |
| :--- | :--- | :--- | :--- |
| **Phase 1: Analyze** | Requirements & Gap Analysis | [`03-Analysis/`](../03-Analysis/) | Benchmark framework comparisons (`HERMES_CLAUDE_CODE_ANALYSIS.md`), gap analysis (`HERMES_PARITY_REPORT.md`), boundary condition research (`HERMES_LOOP_CONDITIONS.md`). |
| **Phase 2: Design** | System & Architectural Design | [`07-Architecture/`](../07-Architecture/) | High-level system architecture, 5-layer loop design (`ITERATION_LOOP_DESIGN.md`), multi-turn workflows (`CONVERSATION_LOOP_WORKFLOW.md`), game-changer feature design (`MAJOR_GAME_CHANGER_FEATURES_ARCH.md`). |
| **Phase 3: Specification** | BDD Specs & Acceptance Criteria | [`08-Specification-Driven-Development/`](../08-Specification-Driven-Development/) | Testable behavior-driven specs (`GAME_CHANGER_FEATURES_SPECS.md`, `AGENT_LOOP_BDD_SPECS.md`) acting as the contract between design and code. |
| **Phase 4: Planning** | Task Backlog & Sprint Tracking | [`14-Tasks/`](../14-Tasks/) | Master task backlog, active sprint items, completion progress (`GAME_CHANGER_FEATURES_TASKS.md`). |
| **Phase 5: Implement** | Code Construction | [`implementations/`](../implementations/) | C# core library & WebAPI/gRPC server wrapper (`implementations/csharp/`), F# core library (`implementations/fsharp/`). |
| **Phase 6: Verify** | Testing & Quality Control Gates | [`09-Testing/`](../09-Testing/) | Automated test suites (Expecto 60/60, xUnit 20/20), quality stop gates (`pre_verify`), continuous integration scripts. |
| **Phase 7: Knowledge** | Knowledge Base & Domain Capture | [`03-Knowledge-System/`](../03-Knowledge-System/) | Domain terminology dictionary, platform sitemap, post-implementation operational knowledge capture (`KNOWLEDGE_INDEX.md`). |

---

## 🛠️ Supporting Engineering Resources

- **`00-Getting-Started/`**: Developer onboarding & environment setup.
- **`01-Engineering-Workflow/`**: Workflow methodology and phase guidelines.
- **`04-Skill-Library/` & `05-Prompt-Library/`**: Reusable agent skills and system prompts.
- **`06-Standards/`**: Coding standards, F# porting guidelines, and commit conventions.
