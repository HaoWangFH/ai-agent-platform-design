# Specification Document: Tool Integration for C# Agent Platform

## 1. Introduction
This specification defines the exact requirements and expected behaviors for the Phase 2 tool integrations in the `Skight.AgentPlatform`.

## 2. Requirements

### 2.1 Git Automation Tool (`GitTools.cs`)
**Requirement**: The agent must be able to interact with Git repositories seamlessly.
- **Specification**:
  - `git_status()`: Returns the current `git status` string. Must handle errors if the directory is not a git repo.
  - `git_commit(message)`: Automatically stages all tracked files and creates a commit with the provided `message`.
  - `git_push()`: Pushes the current branch to origin.
- **Constraints**: 
  - Must only operate within the configured `workspaceRoot`.

### 2.2 Agent Delegation Tool (`DelegateTool.cs`)
**Requirement**: Allow the primary agent to spawn subagents to tackle complex subtasks in parallel or in isolation.
- **Specification**:
  - `delegate_task(role, task)`: 
    - Spawns an ephemeral `AgentRunner`.
    - Seeds the `AgentSessionState` with a system prompt dynamically generated from the `role`.
    - Passes the `task` as the initial user message.
    - Waits for the `TurnResult` up to a maximum depth of `N=10` turns.
    - Returns the `FinalResponse` to the parent agent.
- **Constraints**:
  - Subagents share the parent's `ToolRegistry` but maintain an isolated `AgentSessionState`.

### 2.3 Security Hooks (`ApprovalGuard.cs`)
**Requirement**: Prevent the agent from running destructive terminal commands autonomously.
- **Specification**:
  - A pre-execution interceptor is added to `execute_command`.
  - If the command contains dangerous patterns (e.g., `rm -rf`, `format`, `del /s /q`), the interceptor pauses execution.
  - Returns a payload requesting terminal UI intervention for human approval.

## 3. Acceptance Criteria
- [ ] `GitTools` can successfully stage, commit, and push a test file to a local mock remote.
- [ ] `DelegateTool` can successfully spawn a "Research" subagent that reads a file and summarizes it for the parent.
- [ ] `ApprovalGuard` blocks an attempted `rm -rf *` command and waits for standard input (or mock test input).
