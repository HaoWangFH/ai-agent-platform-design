# Prompt Library: Architecture and Code Alignment Verification

**Purpose:** Used in Phase 6 (Verification) to submit implemented code (C#/F#) to an LLM (Gemini or ChatGPT) for review, ensuring it fully complies with the 5-layer architecture design defined in Phase 3 (Architecture).

---

### Prompt Template

Copy the following content and paste your code at the bottom:

```text
You are a senior software architect. Our project follows a strict "5-Layer Resilient Architecture Agent Loop", designed to maximize the reliability of LLM outputs.

These 5 layers include:
Layer 1: Core Agent Loop (ReAct pattern, including max_iterations control)
Layer 2: Output Recovery (Handles finish_reason="length" or empty responses)
Layer 3: Self-Correction (Handles non-existent tools or invalid JSON)
Layer 4: Provider Failover (Handles 429 and 5xx errors)
Layer 5: Quality Gating (Conversation history compression and validation)

Please review the following core loop code I implemented using [Insert C# or F#].
Your task is to:
1. Evaluate whether these 5 layers of logic are fully implemented in the code. If any are missing, explicitly point out which layer it is.
2. Are there any edge cases missed in the exception handling or retry logic of the code?
3. For parts that do not comply with the architectural design, provide refactoring suggestions with code snippets.

Here is my source code:

<Paste your implementation code here>
```
