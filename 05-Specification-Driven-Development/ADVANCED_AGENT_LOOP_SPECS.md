# Specification: Advanced Agent Loop & Reliability Resilience (BDD Specifications)

> **Specification Standard:** BDD Gherkin & Executable Test Requirements  
> **Target Implementations:** `Skight.AgentPlatform` (C#) & `Skight.AgentPlatform.FSharp` (F#)  
> **Last Updated:** 2026-08-08

---

## 🎯 Specification 1: Length Truncation Continuation (`finish_reason = "length"`)

```gherkin
Feature: Length Truncation Automatic Continuation
  As an AI Agent Platform
  I want the agent loop to detect when an LLM response is truncated due to token limit
  So that the agent seamlessly requests a continuation without losing conversation state or crashing.

  Scenario: LLM response is cut off with finish_reason length
    Given an active agent turn session
    When the LLM API returns finish_reason = "length" with partial content "The implementation details are as follows: function processItem() {"
    Then the agent pipeline should NOT terminate with an error
    And the agent pipeline should append the partial assistant message
    And the agent pipeline should append a user continuation prompt "Your previous response was cut off due to max_tokens limit. Please continue..."
    And the agent pipeline should make a subsequent LLM call to receive the remaining output.

  Scenario: Length continuation retries reach maximum threshold
    Given an active agent turn session with length_continuation_retries = 3
    When the LLM API returns finish_reason = "length" for the 4th consecutive time
    Then the agent pipeline should stop continuing
    And the turn outcome should be Failed with reason "Max length continuation retries exhausted".
```

---

## 🎯 Specification 2: Verification Stop Gate (`pre_verify`)

```gherkin
Feature: Post-Modification Verification Stop Gate
  As a Software Development AI Agent
  I want the agent pipeline to verify file modifications before concluding a turn
  So that code changes are guaranteed to compile and pass tests before declaring completion.

  Scenario: Agent modifies files but attempts to complete without verification
    Given an active agent session
    And the agent executed a tool modifying files "src/Types.fs"
    And no test or build tool was executed after the file modification
    When the LLM API returns a final text response "I have updated the types."
    Then the pipeline should intercept the completion
    And the pipeline should inject a user prompt "You modified files during this turn. Please run tests or build verification commands to ensure your changes work cleanly."
    And the pipeline should execute another iteration to allow the agent to run verification tools.

  Scenario: Agent modifies files and runs verification tool before completing
    Given an active agent session
    And the agent executed a tool modifying files "src/Types.fs"
    And the agent subsequently executed a terminal tool running "dotnet test"
    When the LLM API returns a final text response "I updated the types and verified all tests pass."
    Then the pipeline should accept the response
    And the turn outcome should be Completed with the final text response.
```

---

## 🎯 Specification 3: Message Sequence & Role Alternation Repair

```gherkin
Feature: Automatic Message Sequence & Role Alternation Sanitization
  As an AI Agent Platform
  I want the message history to be sanitized before sending to LLM APIs
  So that invalid message role ordering or orphan tool calls do not cause HTTP 400 Bad Request API crashes.

  Scenario: Assistant message contains tool_calls without matching tool responses
    Given a message history containing:
      | Role      | Content / Details                    |
      | system    | "You are an agent."                  |
      | user      | "Check system status."               |
      | assistant | tool_calls: [id: "tc_1", name: "sys"]|
      | user      | "Cancel that."                       |
    When the message sequence sanitizer processes the history
    Then a synthetic ToolMessage with tool_call_id "tc_1" and content "Error: Tool execution cancelled" should be inserted before the user message
    And the resulting message sequence should be valid for OpenAI API payload submission.
```

---

## 🎯 Specification 4: Multi-Provider LLM Failover Chain

```gherkin
Feature: Multi-Provider LLM API Failover
  As an AI Agent Platform
  I want the LLM caller to automatically try alternative providers on transient failures
  So that rate limits (429) or endpoint outages do not crash agent workflows.

  Scenario: Primary model endpoint returns 429 Rate Limit
    Given a FailoverLlmCaller configured with primary "gpt-4o" and secondary "gpt-4o-mini"
    When the primary caller returns ApiCallFailed "429 Rate Limit Exceeded"
    Then the FailoverLlmCaller should automatically invoke the secondary caller "gpt-4o-mini"
    And if the secondary caller succeeds, the response should be returned seamlessly to the pipeline.
```
