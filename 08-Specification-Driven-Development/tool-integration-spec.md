# Specification Document: Tool Integration for C# & F# Agent Platforms

## 1. Introduction
This specification defines the behavior and acceptance criteria for Phase 2 tools across both C# (OO) and F# (FP) implementations.

## 2. Requirements & BDD Behavior

### 2.1 Git Automation Tool (`git_status`, `git_commit`, `git_push`)
- **Scenario**: Staging and committing workspace changes automatically.
  - **Given** the agent has modified a file inside `workspaceRoot`.
  - **When** the agent calls `git_commit` with message `"Fix issue"`.
  - **Then** all modified files in `workspaceRoot` are staged and committed.
  - **And** the tool returns a success string containing the commit hash.

### 2.2 Subagent Delegation Tool (`delegate_task`)
- **Scenario**: Delegating a complex subtask to an isolated subagent.
  - **Given** the main agent receives a task requiring multi-step investigation.
  - **When** the main agent calls `delegate_task` with `role="Researcher"` and `task="Summarize README"`.
  - **Then** a child agent session is spawned with an isolated message history.
  - **And** the child agent runs up to depth limit 1 before returning its final answer to the parent agent.

### 2.3 Security Guard (`execute_command` Interception)
- **Scenario**: Intercepting destructive commands before execution.
  - **Given** the agent receives a request to delete files recursively (e.g. `rm -rf /` or `del /s /q *`).
  - **When** `execute_command` is invoked with a dangerous payload.
  - **Then** execution is halted before invoking the shell process.
  - **And** an approval prompt is presented to the user interface.

---

## 3. Parity Validation Criteria
- [ ] C# `GitTools.cs` passes XUnit / MSpec BDD specs.
- [ ] F# `GitTools.fs` passes Expecto BDD specs.
- [ ] Both C# and F# delegation implementations prevent infinite subagent nesting (depth <= 1).
