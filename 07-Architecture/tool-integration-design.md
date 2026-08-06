# Design Document: Tool Integration Architecture for C# Agent Platform

## 1. Overview
This document outlines the architectural design for integrating advanced multi-agent and system interaction tools (inspired by Hermes Agent and Claude Code) into the C# Agent Platform (`Skight.AgentPlatform`).

## 2. Core Architecture
The C# Agent Platform uses a modular, object-oriented pipeline to process LLM interactions. 

### 2.1 The Tool Registry (`ToolRegistry.cs`)
The `ToolRegistry` acts as the central hub for all executable tools. It holds a mapping between a string identifier (e.g., `"read_file"`) and an async delegate `Func<string, Task<string>>` alongside the JSON schema (`FunctionDefinition`).

### 2.2 Tool Abstractions
Tools will be grouped into static utility classes or injected services depending on statefulness:
- **Stateless Tools** (e.g., `FileTools.cs`, `TerminalTool.cs`) are implemented as static classes for simplicity.
- **Stateful Tools** (e.g., `McpClient.cs`, `BrowserTool.cs`) require dependency injection or lifecycle management.

## 3. Integration Plan
Based on the Knowledge System analysis, the following capabilities will be designed for Phase 2:

### 3.1 Subagent Delegation (`DelegateTool`)
**Design Pattern**: Mediator / Supervisor Pattern.
- **Concept**: Allow the primary `AgentRunner` to instantiate sub-instances of itself with different system prompts or models.
- **Interface**: `delegate_task(string agentRole, string taskDescription)`
- **Mechanism**: The tool pauses the current context, spins up a new `AgentRunner`, awaits its `TurnResult`, and feeds the final response back as the tool output.

### 3.2 Git Automation (`GitTools`)
**Design Pattern**: Facade Pattern over `TerminalTool`.
- **Concept**: Provide safe, sandboxed operations for Git workflows (similar to Claude Code's `commit-commands`).
- **Interface**: `git_status()`, `git_commit(string message)`, `git_push()`.

### 3.3 Security & Hooks (`ApprovalGuard` / `ToolSecurity`)
**Design Pattern**: Interceptor / Decorator Pattern.
- **Concept**: Intercept sensitive tool executions (e.g., `execute_command` with `rm -rf`) to require human approval or deny based on predefined heuristics.
- **Mechanism**: Implement a pre-execution hook in `ToolRegistry.ExecuteToolAsync`.

## 4. Future Considerations
- **Web Browsing**: Introduce a `BrowserTool` using Playwright for C# to allow DOM inspection and navigation.
- **Memory Management**: Introduce semantic search via a vector database interface for long-term memory retrieval.
