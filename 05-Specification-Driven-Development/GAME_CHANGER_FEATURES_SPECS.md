# BDD Specifications: Game-Changer Agent Features

> **Target Implementations:** `Skight.AgentPlatform` (C#) & `Skight.AgentPlatform.FSharp` (F#)  
> **Last Updated:** 2026-08-09

---

## 🎯 Feature 1: Sub-Agent Task Delegation (`delegate_task`)

```gherkin
Feature: Sub-Agent Task Delegation
  As an AI Agent Platform Architect
  I want the lead agent to delegate sub-tasks to child agents with isolated contexts
  So that complex research and multi-step tasks do not pollute the main conversation history.

  Scenario: Single sub-agent delegation
    Given a parent agent turn session
    When the LLM calls tool delegate_task with goal "Analyze git diff for bug fix"
    Then a child agent loop should be initialized with an isolated AgentSessionState
    And the child agent should execute up to its iteration budget
    And the child agent's final text result should be returned to the parent as a ToolMessage.

  Scenario: Batch parallel sub-agent delegation
    Given a parent agent turn session
    When the LLM calls tool delegate_task with a batch list of 2 task goals
    Then 2 child agent loops should execute concurrently
    And their combined summaries should be returned to the parent as a unified ToolMessage.
```

---

## 🎯 Feature 2: Pre-Verify Code Quality Stop Gate (`pre_verify`)

```gherkin
Feature: Pre-Verify Code Quality Gate
  As an AI Agent Platform
  I want to prevent the agent from completing a turn with unverified file modifications
  So that code changes are guaranteed to pass tests before finishing.

  Scenario: Intercept completed turn when files were modified without test execution
    Given an active agent turn session where files were edited
    And no verification test tool was executed after the edits
    When the agent attempts to output a final text completion
    Then the pipeline should intercept the completion
    And the pipeline should inject a user prompt "You modified files during this turn. Please run tests or build verification commands."
    And the pipeline should execute another iteration.
```
