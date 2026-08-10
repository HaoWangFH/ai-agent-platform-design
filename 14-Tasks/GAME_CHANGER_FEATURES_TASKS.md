# Master Task Backlog: AI Agent Platform Features

> **Target Implementations:** `Skight.AgentPlatform` (C#) & `Skight.AgentPlatform.FSharp` (F#)  
> **Status:** Active Implementation  
> **Last Updated:** 2026-08-09

---

## 📌 Phase 1: Core Game-Changers (Active)

### Task 1: Sub-Agent Task Delegation (`delegate_task`) - COMPLETED
- [x] **1.1 DelegateTool Module**: Implemented `DelegateTool.fs` & `DelegateTool.cs`.
- [x] **1.2 Sub-Agent Runner Binding**: Wired child `runTurnAsync` loop with isolated `AgentSessionState` & bounded iteration budget (5).
- [x] **1.3 Batch Concurrency**: Supported parallel sub-agent fan-out via `Async.Parallel` in F# and `Task.WhenAll` in C#.
- [x] **1.4 BDD Spec Tests**: Implemented `DelegateToolSpecs.fs` (Expecto) and `DelegateToolTests.cs` (xUnit). All tests passed!

### Task 2: Server-Ready Vector Memory (`IMemoryStore`) - COMPLETED
- [x] **2.1 Unified Memory Interface**: Created `IMemoryStore` in C# and `MemoryStore.fs` in F#.
- [x] **2.2 SQLite Embedded Adapter**: Implemented `SqliteMemoryStore` with multi-tenant `UserId` isolation.
- [x] **2.3 Unit & BDD Test Suites**: Added `MemoryStoreSpecs.fs` (Expecto) and `MemoryStoreTests.cs` (xUnit). All tests passed!

### Task 3: Pre-Verify Code Quality Stop Gate (`pre_verify`) - NEXT
- [ ] **3.1 File Mutation Tracker**: Track dirty file states in `AgentSessionState`.
- [ ] **3.2 Interceptor Gate**: Intercept `TurnOutcome.Completed` if files were modified without test execution.
- [ ] **3.3 BDD Spec Tests**: Implement verification gate tests in C# and F#.

### Task 4: Pre-API Steering Drain (`/steer`)
- [ ] **4.1 Steering Queue**: Drain pending steer text before building API payload.
- [ ] **4.2 Tool Output Piggyback**: Append steer text to last tool output to preserve role alternation.

---

## 📌 Phase 2: Advanced Enterprise Capabilities (Extended Roadmap)

### Task 5: Context Compaction Engine (`context_compressor`)
- [ ] **5.1 Token Monitor**: Monitor payload size against token limits.
- [ ] **5.2 Turn Summary Pruner**: Compress older tool outputs into `TurnSummary`.

### Task 6: Interactive Clarification Gateway (`clarify_tool`)
- [ ] **6.1 Structured Choice Tool**: Implement `clarify_tool` schema for interactive decision prompts.

### Task 7: Background Cron & Scheduler (`cronjob_tools`)
- [ ] **7.1 One-Shot & Cron Timers**: Background task scheduler with reactive agent wakeups.
