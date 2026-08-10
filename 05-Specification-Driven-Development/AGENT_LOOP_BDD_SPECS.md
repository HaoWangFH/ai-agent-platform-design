# Agent Iteration Loop — BDD Acceptance Test Specifications (SDD)

> **Related Design Document:** [ITERATION_LOOP_DESIGN.md](../07-Architecture/ITERATION_LOOP_DESIGN.md)

This document extracts specific BDD (Behavior-Driven Development) test cases based on the "5-Layer Resilient Architecture" design. These cases are applied in the automated acceptance testing of both C# and F# implementations (Phase 6 Verification).

## Layer 1: Core Agent Loop (ReAct Pattern)

**Scenario: LLM successfully calls a tool and replies based on the result**
- **Given** a user input containing the question "What is the weather"
- **When** the core loop starts executing
- **And** the LLM decides to call the `get_weather` tool
- **Then** the engine must intercept the tool call, execute the tool, and return the result to the LLM as a `role: tool` message
- **And** the loop continues until the LLM generates a final text reply
- **And** `api_call_count` cannot exceed `max_iterations`

## Layer 2: Output Recovery

**Scenario: Text response is truncated (finish_reason = length)**
- **Given** the LLM generates an ultra-long text
- **When** the API returns a result containing `finish_reason: "length"`
- **Then** the engine should automatically append a prompt to the context (e.g., "Please continue")
- **And** re-trigger the API call, retrying up to 4 times

**Scenario: API returns an empty response**
- **Given** the API call is successful, but the returned content is empty and has no tool calls
- **When** the engine parses the result
- **Then** it must silently retry the request (without adding extra error prompts to the conversation)
- **And** silently retry up to 3 times, then throw an exception or enter fallback logic

## Layer 3: Self-Correction

**Scenario: LLM hallucinates a non-existent tool**
- **Given** the LLM outputs a request to call `get_weather_forecast` (which doesn't exist)
- **When** the engine attempts to dispatch the tool call
- **Then** intercept the call and generate an error result (listing all valid tools)
- **And** feed the error back to the LLM for self-correction in the next turn
- **And** this self-correction retry occurs up to 3 times

**Scenario: Tool call parameters are invalid (Invalid JSON)**
- **Given** the LLM generates non-compliant JSON parameters
- **When** the engine attempts to deserialize the parameters
- **Then** trigger the first stage of silent retry (up to 3 times)
- **And** if it still fails, inject an error result into the context asking the LLM to fix it

## Layer 4: Provider Failover

**Scenario: Encounter Rate Limit (429) or Provider Error (5xx)**
- **Given** the current default provider is OpenAI
- **When** the request returns a 429 Too Many Requests
- **Then** the engine triggers an Exponential Backoff retry
- **And** if multiple retries fail, seamlessly failover to a backup provider (e.g., Azure OpenAI)

## Layer 5: Quality Gating

**Scenario: Conversation history exceeds context window limit**
- **Given** the number of context tokens in the conversation approaches the model's limit
- **When** preparing to make the next API call
- **Then** trigger the compression strategy, removing the oldest messages but retaining the system prompt and the latest QA pair
