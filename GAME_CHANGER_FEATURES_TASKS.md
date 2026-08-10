# Task Backlog: 4 Game-Changer Agent Features

> **Target Implementations:** `Skight.AgentPlatform` (C#) & `Skight.AgentPlatform.FSharp` (F#)  
> **Status:** Active Implementation  
> **Last Updated:** 2026-08-09

---

## 📌 Task 1: Sub-Agent Task Delegation (`delegate_task`) - IN PROGRESS
- [ ] **1.1 DelegateTool Module**: Implement `DelegateTool.fs` & `DelegateTool.cs`.
- [ ] **1.2 Sub-Agent Runner Binding**: Wire child `runTurnAsync` loop with isolated `AgentSessionState` & bounded iteration budget (5).
- [ ] **1.3 Batch Concurrency**: Support parallel sub-agent fan-out via `Async.Parallel` in F# and `Task.WhenAll` in C#.
- [ ] **1.4 BDD Spec Tests**: Implement `DelegateToolSpecs.fs` (Expecto) and `DelegateToolTests.cs` (xUnit).

---

## 📌 Task 2: Pre-Verify Code Quality Stop Gate (`pre_verify`)
- [ ] **2.1 File Mutation Tracker**: Track dirty file states in `AgentSessionState`.
- [ ] **2.2 Interceptor Gate**: Intercept `TurnOutcome.Completed` if files were modified without test execution.
- [ ] **2.3 BDD Spec Tests**: Implement verification gate tests in C# and F#.

---

## 📌 Task 3: Pre-API Steering Drain (`/steer`)
- [ ] **3.1 Steering Queue**: Drain pending steer text before building API payload.
- [ ] **3.2 Tool Output Piggyback**: Append steer text to last tool output to preserve role alternation.

---

## 📌 Task 4: Persistent Ephemeral Vector Memory (`memory_manager`)
- [ ] **4.1 Persistent Store**: Key-value & vector embedding memory manager.
- [ ] **4.2 Ephemeral Injection**: Inject memory context without polluting session database.
