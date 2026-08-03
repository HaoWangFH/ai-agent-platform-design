# Hermes Agent Loop: Continue & Exit Conditions — Source Code Analysis

> **Source File:** [conversation_loop.py](file:///c:/Users/hwang5/wiki/raw/projects/hermes-agent/agent/conversation_loop.py) (5,356 lines, 310KB)  
> **Main Loop:** Line 643 `while (api_call_count < agent.max_iterations and agent.iteration_budget.remaining > 0) or agent._budget_grace_call:`  
> **Inner Retry Loop:** Line 1105 `while retry_count < max_retries:`  
> **Last Updated:** 2026-08-03

---

## 1. Loop Entry Condition (Line 643)

```python
while (api_call_count < agent.max_iterations 
       and agent.iteration_budget.remaining > 0
      ) or agent._budget_grace_call:
```

Three conditions govern entry:

| Condition | Purpose |
|---|---|
| `api_call_count < agent.max_iterations` | Hard ceiling on API calls per turn (default 10) |
| `agent.iteration_budget.remaining > 0` | Soft budget system (cross-session cumulative limit) |
| `agent._budget_grace_call` | One extra "grace" iteration when budget is exactly 0, consumed on entry |

**Gap vs. our implementation:** Our loop only checks `apiCalls < maxIterations`. We don't have `IterationBudget` (cross-session cumulative) or `_budget_grace_call`.

---

## 2. All `continue` Triggers (Loop Recurses / Iterates Again)

These are the 12 distinct `continue` sites in the main `while` loop that cause the agent to make another API call:

### 2A. Pre-API Context Compression (Line 1063)
```python
# Pre-API pressure check: token estimate exceeds threshold
if _compressor.should_compress(request_pressure_tokens):
    messages = agent._compress_context(messages, ...)
    api_call_count -= 1  # refund the iteration
    continue
```
**What:** Token-level context compression using a summary LLM. Refunds the iteration so the compressed request gets a fresh chance.  
**Gap:** Our implementation uses simple message-count trimming, not token-level pressure estimation or summary-LLM compression.

### 2B. Nous Rate Limit → Fallback (Line 1133)
```python
if agent._try_activate_fallback():
    retry_count = 0
    continue
```
**What:** When the Nous Portal rate limiter fires, try to activate a fallback provider and retry.  
**Gap:** Not implemented (multi-provider failover).

### 2C. Inner Retry Loop: Provider-Specific Retries (Line 1489, 1562, 1718, etc.)
Multiple `continue` sites inside `while retry_count < max_retries:` for:
- **Transient API errors** with exponential backoff (our equivalent of Step 2.4)
- **Rate limit (429)** with adaptive backoff
- **Credential pool rotation** on auth errors
- **Fallback provider activation** on billing/content-policy errors
- **Image dimension downscaling** on provider-reported image size errors

**Gap:** We implement basic retry with backoff. We don't have credential rotation, fallback chains, or image downscaling.

### 2D. Length Truncation → Continuation Retry (Line 1949)
```python
if finish_reason == "length" and not _trunc_has_tool_calls:
    length_continue_retries += 1
    messages.append(continue_msg)  # "Please continue..."
    _retry.restart_with_length_continuation = True
    break  # breaks inner retry loop, continues outer loop
```
**What:** When `finish_reason="length"`, append a "please continue" user message and loop again (up to 4 times). Also handles truncated tool call arguments separately.  
**Gap:** **Not implemented** — our loop doesn't check `finish_reason` at all.

### 2E. Truncated Tool Call → Retry API Call (Line 2000)
```python
if truncated_tool_call_retries < 4:
    truncated_tool_call_retries += 1
    agent._ephemeral_max_output_tokens = min(_tc_boost, _tc_boost_cap)
    continue  # re-run same API call with higher max_tokens
```
**What:** Retry the same API call (without appending the broken response) with progressively larger `max_tokens` budgets.  
**Gap:** Not implemented.

### 2F. Invalid Tool Name → Self-Correction (Line 4527)
```python
if invalid_tool_calls:
    # Append error tool results listing available tools
    for tc in assistant_message.tool_calls:
        messages.append({"role": "tool", "content": f"Tool '{name}' does not exist. ..."})
    continue
```
**What:** When LLM hallucinates a tool name, send error tool results and loop again for self-correction. After 3 retries, give up.  
**Parity:** ✅ **Implemented** — our implementations do this (but without the 3-retry cap or fuzzy tool name repair).

### 2G. Invalid JSON Arguments → Retry / Recovery (Line 4590, 4619)
```python
if agent._invalid_json_retries < 3:
    continue  # silent retry of same API call
else:
    # Inject recovery tool error results
    messages.append({"role": "tool", "content": "Error: Invalid JSON arguments..."})
    continue  # loop again with error feedback
```
**What:** Two-phase recovery: first silently retry (up to 3x), then inject error tool results.  
**Parity:** ⚠️ **Partial** — we inject error results immediately (no silent retry phase).

### 2H. Tool Execution → Continue Loop (Line 4852)
After executing all tool calls successfully:
```python
# Append all tool results
for tc in assistant_message.tool_calls:
    result = handle_function_call(tc, ...)
    messages.append({"role": "tool", "content": result})
continue  # send tool results to LLM for next response
```
**Parity:** ✅ **Implemented** — this is the primary tool loop.

### 2I. Empty Response → Retry with Nudge (Line 5008)
```python
if _truly_empty and agent._empty_content_retries < 3:
    agent._empty_content_retries += 1
    continue  # retry same request (no nudge message injected in Hermes!)
```
**What:** Up to 3 retries for truly empty responses. If still empty after 3, try fallback provider. If no fallback, return `"(empty)"`.  
**Parity:** ⚠️ **Partial** — we inject a nudge user message; Hermes does a silent retry. Hermes also tries fallback providers before giving up.

### 2J. Empty Response → Fallback Provider (Line 5040)
```python
if _truly_empty and agent._fallback_chain:
    if agent._try_activate_fallback():
        agent._empty_content_retries = 0
        continue
```
**Gap:** Not implemented (multi-provider failover).

### 2K. Verification Stop Gate → Pre-Verify Nudge (Line 5266)
```python
# Agent modified files but didn't run tests/builds
messages.append({"role": "user", "content": _verify_nudge})
_pending_verification_response = final_response
final_response = None
continue
```
**What:** Intercepts the final text response if the agent modified files during the turn. Injects a "please verify" nudge and loops again, holding the original response as pending.  
**Gap:** **Not implemented**.

### 2L. Intent Ack Continuation (Codex Recovery)
```python
# Model said "Sure, I'll do that" without making tool calls
messages.append({"role": "user", "content": "Please proceed..."})
continue
```
**Gap:** **Not implemented**.

---

## 3. All `break` / `return` Exit Conditions

| Line | Condition | Exit Reason | In Our Impl? |
|---|---|---|:---:|
| 648–653 | `agent._interrupt_requested` | `interrupted_by_user` | ✅ |
| 664–668 | `iteration_budget.consume()` fails | `budget_exhausted` | ✅ (simplified) |
| 974–986 | Ollama runtime context too small | `ollama_runtime_context_too_small` | ❌ |
| 1138–1150 | Nous rate limit + no fallback | `nous_rate_limit` | ❌ |
| 1843–1850 | Thinking budget exhausted | `thinking_budget_exhausted` | ❌ |
| 1954–1961 | Length continuation exhausted (4 attempts) | `partial` | ❌ |
| 2323 | MoA aggregation complete | `moa_text_response` | ❌ |
| 3506 | Streaming complete | `text_response` | ✅ |
| 4435–4442 | Codex incomplete after 3 retries | `codex_incomplete` | ❌ |
| 4482–4490 | Invalid tool name after 3 retries | `invalid_tool_exhausted` | ❌ |
| 4572–4579 | Truncated tool args → give up | `truncated_tool_args` | ❌ |
| 5048–5087 | Empty response exhausted + no fallback | `empty_response_exhausted` | ⚠️ (partial) |
| 5270–5273 | Clean text response | `text_response` | ✅ |
| 5324–5330 | Error near max_iterations | `error_near_max_iterations` | ❌ |

---

## 4. Feature Parity Matrix — Loop Conditions Only

| # | Loop Condition / Trigger | Hermes Location | Platform Status | Notes |
|---|---|---|:---:|---|
| 1 | Main loop entry: `api_call_count < max_iterations` | L643 | ✅ Implemented | Direct equivalent |
| 2 | Iteration budget (cross-session cumulative) | L643, L664 | ❌ Missing | Only hard `maxIterations` limit |
| 3 | Budget grace call (one extra iteration) | L643, L662 | ❌ Missing | |
| 4 | Interrupt guard | L648 | ✅ Implemented | |
| 5 | Step callback (gateway hooks) | L671 | ❌ Missing | Plugin infrastructure |
| 6 | Skill nudge counter | L700 | ❌ Missing | Skill system not implemented |
| 7 | `/steer` drain before API call | L716 | ❌ Missing | Steering not implemented |
| 8 | Tool call argument sanitization | L761 | ❌ Missing | |
| 9 | Message sequence repair | L783 | ❌ Missing | Role alternation repair |
| 10 | API payload isolation (shallow copy) | L792 | ✅ Implemented | |
| 11 | Ephemeral context injection (memory, plugins) | L796 | ❌ Missing | Memory system not implemented |
| 12 | Anthropic prompt caching (`cache_control`) | L870 | ❌ Missing | |
| 13 | Sanitize orphan tool results | L905 | ❌ Missing | |
| 14 | Drop thinking-only assistant turns | L915 | ❌ Missing | |
| 15 | Normalize whitespace for KV cache reuse | L926 | ❌ Missing | |
| 16 | Token-level context compression (summary LLM) | L1012–1063 | ❌ Missing | We do simple message-count trimming |
| 17 | Inner retry loop with exponential backoff | L1105 | ✅ Implemented | |
| 18 | Nous rate limit guard + fallback | L1111 | ❌ Missing | |
| 19 | Credential pool rotation on 401 | L1489 | ❌ Missing | |
| 20 | Multi-provider fallback on 429/billing | L1562 | ❌ Missing | |
| 21 | Length truncation continuation (4 retries) | L1904–1949 | ❌ Missing | `finish_reason="length"` not checked |
| 22 | Truncated tool call retry (boost max_tokens) | L1965–2000 | ❌ Missing | |
| 23 | Content filter → fallback | L1869–1895 | ❌ Missing | |
| 24 | Thinking budget exhaustion detection | L1791–1850 | ❌ Missing | |
| 25 | Tool name fuzzy repair | L4459 | ❌ Missing | We return error; Hermes tries auto-repair first |
| 26 | Invalid tool name self-correction (3 retries) | L4467–4527 | ⚠️ Partial | We self-correct but no retry cap |
| 27 | Invalid JSON silent retry (3x) then recovery | L4551–4619 | ⚠️ Partial | We inject error immediately |
| 28 | Post-tool guardrails (dedup, cap delegate_task) | L4625–4630 | ❌ Missing | |
| 29 | Housekeeping tool detection + mute | L4646–4655 | ❌ Missing | |
| 30 | Tool execution → continue loop | L4852 | ✅ Implemented | |
| 31 | Empty response retry (3x silent) | L4997–5008 | ⚠️ Partial | We inject nudge; Hermes retries silently |
| 32 | Empty response → fallback provider | L5016–5040 | ❌ Missing | |
| 33 | Codex incomplete ack recovery | L4400–4431 | ❌ Missing | |
| 34 | Verification stop gate (`pre_verify`) | L5233–5266 | ❌ Missing | |
| 35 | Outer exception → fill missing tool results | L5290–5315 | ❌ Missing | |
| 36 | Error near max_iterations → graceful exit | L5324 | ❌ Missing | |
| 37 | Clean text response → exit | L5270–5273 | ✅ Implemented | |

### Summary

| Status | Count |
|---|:---:|
| ✅ **Fully Implemented** | 7 |
| ⚠️ **Partially Implemented** | 4 |
| ❌ **Not Implemented** | 26 |
| **Total Distinct Loop Conditions** | **37** |
