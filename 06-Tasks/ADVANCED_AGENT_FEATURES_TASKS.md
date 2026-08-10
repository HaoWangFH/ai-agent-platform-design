# Prioritized Task Backlog: Advanced Hermes & Claude Code Features

> **Target Platform:** `Skight.AgentPlatform` (C#) & `Skight.AgentPlatform.FSharp` (F#)  
> **Status:** Open & Prioritized  
> **Last Updated:** 2026-08-08

---

## 📌 Phase 1: High-Priority Reliability Features (P0 - Immediate Pick-Up)

### Task 1: Length Truncation Continuation (`finish_reason = "length"`)
- [ ] **1.1 Domain Signature Update**: Add `FinishReason` (e.g. `Stop`, `Length`, `ToolCalls`, `ContentFilter`) to `LlmTurnResponse` in `Types.fs` & `Types.cs`.
- [ ] **1.2 Pipeline Interceptor**: Implement `handleLengthContinuation` in `AgentPipeline.fs` / `AgentPipeline.cs`.
- [ ] **1.3 Continuation Prompt Nudge**: Append partial response and `"Your previous response was cut off due to max_tokens limit. Please continue..."` prompt.
- [ ] **1.4 Unit & Spec Tests**: Create Expecto and xUnit tests verifying length truncation auto-continuation (up to 4 retries).

### Task 2: Verification Stop Gate (`pre_verify`)
- [ ] **2.1 Turn State Dirty Flag**: Track `FilesModifiedInTurn: bool` and `VerificationExecutedInTurn: bool` in `TurnState`.
- [ ] **2.2 Interceptor Guard**: Intercept `TurnOutcome.Completed` if `FilesModifiedInTurn` is true and `VerificationExecutedInTurn` is false.
- [ ] **2.3 Verification Nudge**: Inject prompt `"You modified files during this turn. Please run tests or build verification commands to ensure your changes work cleanly."`
- [ ] **2.4 Spec Tests**: Write Expecto and xUnit specs verifying that modifying files without running tests triggers the verification nudge.

---

## 📌 Phase 2: Message & Payload Integrity (P1 - High Priority)

### Task 3: Message Sequence & Role Alternation Sanitizer
- [ ] **3.1 Sanitizer Module**: Implement `MessageSequenceSanitizer.fs` / `MessageSequenceSanitizer.cs`.
- [ ] **3.2 Orphan Tool Result Injection**: Detect tool calls in assistant messages without matching tool responses; inject synthetic tool error messages before subsequent user/system messages.
- [ ] **3.3 Pre-Flight Serialization Hook**: Wrap `preparePayload` with `sanitizeMessageSequence` before calling LLM APIs.
- [ ] **3.4 Spec Tests**: Write tests ensuring no `400 Bad Request` payload errors on corrupted message sequences.

### Task 4: Multi-Provider LLM Failover Chain
- [ ] **4.1 Failover Decorator**: Implement `FailoverLlmCaller` wrapping a primary and fallback `LlmCaller`.
- [ ] **4.2 Transient Error Trigger**: Automatically attempt fallback model on `429 Rate Limit`, `500 Internal Error`, or `ApiCallFailed`.
- [ ] **4.3 Integration Tests**: Create mock tests demonstrating seamless failover when primary endpoint fails.

---

## 📌 Phase 3: Advanced Controls & Delegation (P2/P3 - Future Enhancements)

### Task 5: Dynamic Tool Filter & Context Masking (P2)
- [ ] **5.1 Contextual Tool Filter**: Expose `FilterToolsByPhase(phase)` on `ToolRegistry`.
- [ ] **5.2 Read-Only Scope**: Support masking file/terminal edit tools during research-only turns.

### Task 6: Token-Level Summary Compression (P2)
- [ ] **6.1 Token Estimator**: Add token estimation utility.
- [ ] **6.2 Summary LLM Integration**: Implement LLM-based middle-history summarization when approaching context window limits.

### Task 7: Sub-Agent Task Delegation with Isolated Context (P3)
- [ ] **7.1 SubAgentDelegator**: Implement tool allowing an agent to spawn a child `runTurnAsync` session with bounded budget and isolated history.
- [ ] **7.2 Parent-Child State Sync**: Return condensed sub-agent task results to the main session.
