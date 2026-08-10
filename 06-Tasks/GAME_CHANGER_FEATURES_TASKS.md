# Master Task Backlog: AI Agent Platform Features

> **Target Implementations:** `Skight.AgentPlatform` (C#) & `Skight.AgentPlatform.FSharp` (F#)  
> **Status:** Active Implementation  
> **Last Updated:** 2026-08-10

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

### Task 3: Pre-Verify Code Quality Stop Gate (`pre_verify`) - COMPLETED
- [x] **3.1 File Mutation Tracker**: Track dirty file states in `AgentSessionState` & `TurnState`.
- [x] **3.2 Interceptor Gate**: Intercept `TurnOutcome.Completed` if files were modified without test execution (`max_nudges = 2`).
- [x] **3.3 Unit & BDD Test Suites**: Added `PreVerifySpecs.fs` (Expecto) and `PreVerifyTests.cs` (xUnit). All tests passed!

### Task 4: Pre-API Steering Drain (`/steer`) - COMPLETED
- [x] **4.1 Steering Queue**: Drain pending steer text before building API payload (`ConcurrentQueue<string>`).
- [x] **4.2 Tool Output Piggyback**: Append steer text to last tool output to preserve role alternation (`user -> assistant -> tool -> user`).
- [x] **4.3 Unit & BDD Test Suites**: Added `SteeringSpecs.fs` (Expecto) and `SteeringTests.cs` (xUnit). All tests passed!

---

## 📌 Phase 2: Advanced Enterprise Capabilities (Extended Roadmap)

### Task 5: Context Compaction Engine (`context_compressor`) - COMPLETED
- [x] **5.1 Token Budget Monitor**: Automatically detect when conversation length exceeds 80% of `ContextWindowLimit`.
- [x] **5.2 Turn Summary Pruner**: Compact older conversation history into `TurnSummary` system message while preserving system prompt and recent context.
- [x] **5.3 Unit & BDD Test Suites**: Added `ContextCompressorSpecs.fs` (Expecto) and `ContextCompressorTests.cs` (xUnit). All tests passed!

### Task 6: Interactive Clarification Gateway (`clarify_tool`) - COMPLETED
- [x] **6.1 Structured Choice Tool**: Implemented `clarify_tool` schema for interactive decision prompts (`question`, `options`, `is_multi_select`).
- [x] **6.2 Non-Interactive Fallback**: Built automatic default choice fallback for non-interactive / headless agent execution environments.
- [x] **6.3 Unit & BDD Test Suites**: Added `ClarifyToolSpecs.fs` (Expecto) and `ClarifyToolTests.cs` (xUnit). All tests passed!

### Task 7: Background Cron & Scheduler (`cronjob_tools`)
- [ ] **7.1 One-Shot & Cron Timers**: Background task scheduler with reactive agent wakeups.
