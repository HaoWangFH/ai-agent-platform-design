# TypeScript Agent Implementation

> **Mapping to Abstract Workflow:** [CONVERSATION_LOOP_WORKFLOW.md](../../CONVERSATION_LOOP_WORKFLOW.md)

## Overview

The TypeScript implementation models the 4-phase Agent Conversation Loop workflow using async/await, strong TypeScript interfaces, and the official `openai` NPM package.

## File Structure

- `src/Agent.ts`: `Agent` class, `TurnResult` interface, and `AgentConfig` interface.
- `src/ToolRegistry.ts`: `ToolRegistry` for registering and executing TypeScript tools with JSON schemas.
- `src/tools.ts`: Tool implementations.
- `src/index.ts`: Readline-based interactive CLI loop.

## Workflow Mapping

### 1. Phase 1: Turn Prologue
- Implemented in `Agent.run(userInput: string): Promise<TurnResult>`.
- Appends user prompt to `this.messages`.
- Resets per-turn variables: `apiCalls = 0`, `this.interruptRequested = false`, `emptyContentRetries = 0`.

### 2. Phase 2: Main Conversation Loop
- **2.1 Pre-API Checks:** Checked at start of `while (apiCalls < this.maxIterations)`. Checks `this.interruptRequested` and `maxIterations`.
- **2.2 Message Preparation:** `prepareApiMessages()` shallow-copies `this.messages`.
- **2.3 Context Window Protection:** `compressContextIfNeeded()` trims middle history when message count > `contextWindowLimit`.
- **2.4 Inner Retry Loop:** Async retry loop with `await new Promise(...)` exponential delay.
- **2.5 Response Normalization:** Extracts `response.choices[0].message`.
- **2.6 Tool Execution Path:**
  - Self-corrects unregistered tool calls with diagnostic tool result.
  - Safely parses JSON parameters with `JSON.parse`.
  - Executes tool wrapped in `try...catch`.
  - Appends tool results (`role: 'tool'`) and continues loop.
- **2.7 Final Text Response Path:**
  - Handles empty text responses with prompt nudges before returning fallback text.
  - Returns `TurnResult` with `completed: true, exitReason: 'text_response'`.

### 3. Phase 3 & 4: Turn Finalization
- Returns structured `TurnResult` object.
