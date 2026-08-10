# Design Document: Dual-Paradigm Tool Integration Architecture (C# & F#)

## 1. Overview
This document defines the architecture for integrating Phase 2 core and extended tools (Subagent Delegation, Git Automation, and Security Hooks) into the AI Agent Platform. To support language parity, the design accommodates both **Object-Oriented (C#)** and **Functional (F#)** paradigms.

---

## 2. Paradigm & Design Patterns

| Feature Component | C# (Object-Oriented) Pattern | F# (Functional) Pattern |
| :--- | :--- | :--- |
| **Tool Abstraction** | Static utility classes / Interfaces | Pure modules & functions (`let` bindings) |
| **State Management** | Mutable `AgentSessionState` class | Immutable `AgentSession` record |
| **Subagent Delegation** | Mediator / Supervisor Pattern (`DelegateTool.cs`) | Function composition & recursive loop invocation (`DelegateTool.fs`) |
| **Security Interceptors** | Decorator Pattern over `ToolRegistry` (`ApprovalGuard.cs`) | Active Patterns / Result type wrapping (`ApprovalGuard.fs`) |
| **Git Operations** | Facade Pattern over shell process runner | Pure wrapper functions taking execution delegates |

---

## 3. Component Design

### 3.1 Tool Registry (`ToolRegistry`)
- **C#**: `ToolRegistry` stores `Func<string, Task<string>>` handlers associated with OpenAPI/JSON schemas.
- **F#**: `ToolRegistry` is a record or module wrapping a Map of tool name to handler function `string -> Async<string>`.

### 3.2 Subagent Delegation (`delegate_task`)
- **C#**: Spawns an `AgentRunner` instance with custom system prompt, executes `RunTurnLoopAsync`, returns final string response.
- **F#**: Takes current context, constructs an isolated `AgentSession` record, calls `runTurnLoop`, and extracts the final message asynchronously.

### 3.3 Security & Approval Guard (`ApprovalGuard`)
- **C#**: Intercepts `execute_command` before running. If dangerous pattern is matched, triggers user approval callback.
- **F#**: Evaluates command via Active Pattern `(|DangerousCommand|_|)`. If matched, yields `ApprovalRequired` union case.

---

## 4. Parity & File Structure

```
implementations/
├── csharp/src/Skight.AgentPlatform/
│   ├── GitTools.cs
│   ├── DelegateTool.cs
│   └── ApprovalGuard.cs
└── fsharp/src/Skight.AgentPlatform.FSharp/
    ├── GitTools.fs
    ├── DelegateTool.fs
    └── ApprovalGuard.fs
```
