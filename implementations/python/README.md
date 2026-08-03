# Python Agent Implementation

> **Mapping to Abstract Workflow:** [CONVERSATION_LOOP_WORKFLOW.md](../../CONVERSATION_LOOP_WORKFLOW.md)

## Overview

The Python implementation follows the 4-phase Agent Conversation Loop workflow using Python dataclasses, type hints, and standard OpenAI SDK.

## File Structure

- `agent.py`: `Agent` class and `TurnResult` dataclass implementing the 4-phase loop.
- `registry.py`: `ToolRegistry` for tool schema extraction and tool execution.
- `tools.py`: Tool definitions registered via decorator/method calls.
- `main.py`: Interactive CLI loop entry point.

## Workflow Mapping

### 1. Phase 1: Turn Prologue
- Initialized in `Agent.run(user_input: str) -> TurnResult`.
- Appends user input to `self.messages`.
- Resets per-turn counters: `api_call_count = 0`, `self._interrupt_requested = False`, `empty_content_retries = 0`.

### 2. Phase 2: Main Conversation Loop
- **2.1 Pre-API Checks:** Checked at start of `while api_call_count < self.max_iterations:`. Checks `_interrupt_requested` and budget limit.
- **2.2 Message Preparation:** `_prepare_api_messages()` shallow-copies `self.messages` to produce `prepared_messages`.
- **2.3 Context Window Protection:** `_compress_context_if_needed()` trims middle history when `len(messages) > context_window_limit`.
- **2.4 Inner Retry Loop:** `for retry in range(self.max_retries)` with `time.sleep(2 ** retry)`.
- **2.5 Response Normalization:** Accesses `response.choices[0].message`.
- **2.6 Tool Execution Path:** 
  - Validates `name in registry._tools` (unregistered tool self-correction).
  - Validates JSON parse via `json.loads`.
  - Executes tool with `try...except` exception handling.
  - Appends tool results (`role="tool"`) and continues loop.
- **2.7 Final Text Response Path:**
  - Empty response recovery with prompt nudge.
  - Returns `TurnResult(completed=True, exit_reason="text_response")`.

### 3. Phase 3 & 4: Turn Finalization
- Returns structured `TurnResult` object.
