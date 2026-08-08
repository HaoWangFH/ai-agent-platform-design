# Architectural Analysis: Hermes Agent & Claude Code Advanced Features

> **Document Version:** 1.0.0  
> **Target Platform:** Skight AI Agent Platform (C# & F#)  
> **Last Updated:** 2026-08-08

---

## 🧭 Executive Summary

Following our foundational implementations of the pure functional `RunAsync` loop, MCP protocol client, tool security sandbox, human-in-the-loop approval guard, and media/automation tools, this analysis benchmarks the **Hermes Agent** open-source architecture and **Claude Code** CLI agent to identify high-value, enterprise-grade capabilities for implementation.

By analyzing both architectures, we have extracted **7 Core Feature Patterns** missing from conventional agent platforms. These features significantly increase agent reliability, resilience against API errors/truncations, self-verification quality, and multi-agent scalability.

---

## 🔬 Comparative Analysis Matrix

| Feature Pattern | Hermes Agent Mechanism | Claude Code Mechanism | Proposed Platform Architecture | Impact | Priority |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **1. Length Truncation Continuation** | Checks `finish_reason == "length"`, appends `"Please continue..."` & loops (4x cap) | Auto-streams continuation requests preserving partial tool call JSON fragments | `LengthContinuationHandler`: Intercepts `length` exit, stitches partial fragments, appends continuation nudge | 🔴 High | **P0** |
| **2. Verification Stop Gate (`pre_verify`)** | Detects file modifications (`file_edited=True`), holds final response & injects verification nudge | Runs automated post-edit linting/test checks before yielding turn | `VerificationGate`: Tracks dirty file states during turn; if unverified, nudges agent to run tests before completing | 🔴 High | **P0** |
| **3. Message Sequence & Role Repair** | Auto-injects synthetic tool responses for orphaned `tool_call_id`s before API payload serialization | Strict message stack sanitizer ensuring alternating `user/assistant/tool` order | `MessageSequenceSanitizer`: Pre-flight payload transformation that repairs orphan tool calls and illegal consecutive role turns | 🟡 Med | **P1** |
| **4. Multi-Provider & Model Failover** | `_try_activate_fallback()` cycles models on 429/500/content-policy errors | Seamless fallback across primary and secondary model endpoints | `FailoverLlmCaller`: Decorator wrapping `LlmCaller` with fallback model chain on transient/quota errors | 🟡 Med | **P1** |
| **5. Dynamic Tool Masking & Scope** | Mutes housekeeping tools after completion; caps recursive `delegate_task` | Restricts tools by execution phase (e.g. read-only tools during research, edit tools during coding) | `DynamicToolFilter`: Phase-aware tool registry filter that exposes only allowable schemas per turn | 🟡 Med | **P2** |
| **6. Token-Level Summary Compression** | `should_compress(tokens)` -> LLM summary of middle conversation history | `/compact` command & auto-summarization at context pressure thresholds | `SummaryContextCompressor`: Replaces message-count trimming with LLM-generated conversation summary | 🟢 Low | **P2** |
| **7. Sub-Agent Isolation & Delegation** | Spawns child agent loops with isolated budgets | Spawns child CLI sessions for broad research/bash execution with independent context | `SubAgentDelegator`: Invokes isolated `runTurnAsync` child session with bounded iteration budget & clean context | 🟢 Low | **P3** |

---

## 📐 Detailed Design Specifications for Top Priority Features

### Feature 1: Length Truncation Continuation (P0)

#### Problem
When the LLM output reaches `max_tokens` limit, the API returns `finish_reason = "length"`. Conventional agents crash or return incomplete responses/broken JSON arguments.

#### Architecture
```fsharp
type LengthContinuationState = {
    ContinuationAttempts: int
    MaxAttempts: int
}

let handleLengthTruncation (response: LlmTurnResponse) (state: TurnState) : StepResult<TurnState, TurnResult> =
    if response.FinishReason = "length" then
        if state.LengthRetries < 4 then
            let nudgeMsg = UserMessage "Your previous response was cut off due to max_tokens limit. Please continue exactly from where you left off."
            Continue { state with Messages = state.Messages @ [ AssistantMessage(response.Content, response.ToolCalls); nudgeMsg ]
                                 LengthRetries = state.LengthRetries + 1 }
        else
            Exit { Outcome = TurnOutcome.Failed (FailureReason.NoResponse "Max length continuation retries exhausted"); Messages = state.Messages; ApiCalls = state.ApiCalls }
    else
        Continue state
```

---

### Feature 2: Verification Stop Gate (`pre_verify`) (P0)

#### Problem
AI Agents frequently edit files or write code, but complete turns without checking if the code compiles or tests pass, delivering broken code to the user.

#### Architecture
```fsharp
type VerificationState = {
    FilesModifiedInTurn: bool
    VerificationPerformed: bool
}

let checkVerificationGate (filesModified: bool) (verificationPerformed: bool) (responseContent: string) (state: TurnState) : StepResult<TurnState, TurnResult> =
    if filesModified && not verificationPerformed then
        printfn "  [Verification Gate] Agent modified files but did not run verification tests. Injecting nudge..."
        let verifyNudge = UserMessage "You modified files during this turn. Please run tests or build verification commands to ensure your changes work cleanly before completing."
        Continue { state with Messages = state.Messages @ [ AssistantMessage(responseContent, []); verifyNudge ] }
    else
        Exit { Outcome = TurnOutcome.Completed responseContent; Messages = state.Messages; ApiCalls = state.ApiCalls }
```

---

### Feature 3: Message Sequence & Role Repair (P1)

#### Problem
OpenAI / Azure API calls fail with `400 Bad Request` if `tool` messages do not immediately follow an `assistant` message with matching `tool_calls`, or if role sequences are corrupted.

#### Architecture
```csharp
public static List<AgentMessage> SanitizeMessageSequence(List<AgentMessage> messages)
{
    var sanitized = new List<AgentMessage>();
    var pendingToolIds = new HashSet<string>();

    foreach (var msg in messages)
    {
        if (msg is AssistantMessage assistant)
        {
            foreach (var tc in assistant.ToolCalls) pendingToolIds.Add(tc.Id);
            sanitized.Add(msg);
        }
        else if (msg is ToolMessage tool)
        {
            if (pendingToolIds.Contains(tool.ToolCallId))
            {
                pendingToolIds.Remove(tool.ToolCallId);
                sanitized.Add(msg);
            }
            // Drop orphan tool messages without matching assistant tool_calls
        }
        else
        {
            // Inject dummy tool responses for any unfulfilled tool calls before system/user message
            foreach (var missingId in pendingToolIds)
            {
                sanitized.Add(new ToolMessage(missingId, "Error: Tool execution cancelled or missing result."));
            }
            pendingToolIds.Clear();
            sanitized.Add(msg);
        }
    }
    return sanitized;
}
```

---

## 🎯 Verification Criteria & Success Metrics

1. **Length Continuation**: Zero JSON truncation crashes when LLM output hits token cap.
2. **Verification Gate**: Agent automatically invokes test/build tools whenever files are modified before issuing `Completed`.
3. **Message Sanitizer**: 0% `400 Invalid Message Sequence` API errors across all multi-turn sessions.
4. **Multi-Provider Failover**: Zero turn failures when primary API endpoint returns `429 Rate Limit`.
