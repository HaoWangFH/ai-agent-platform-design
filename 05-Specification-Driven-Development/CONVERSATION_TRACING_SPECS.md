# BDD Specification: Conversation Tracing & Observability System

## Feature: Conversation Tracing, Transcript Logging, and OpenTelemetry Export

As a platform engineer and developer,
I want to trace every conversation turn, LLM API call, tool execution, and sub-agent delegation,
So that I can replay conversations, inspect execution flows, debug failures, and monitor performance with zero overhead when disabled.

---

### Scenario 1: Non-Blocking Telemetry Record when Enabled
  Given telemetry configuration `Telemetry.Enabled = true`
  When an agent receives user input and executes a turn loop
  Then a root session span `agent.session` and turn span `agent.turn` MUST be created
  And LLM API calls MUST produce `llm.call` spans containing token usage metrics
  And tool executions MUST produce `tool.execution` spans containing tool name and duration
  And all events MUST be enqueued into an in-memory channel without blocking the main execution thread.

### Scenario 2: Zero-Overhead Execution when Disabled
  Given telemetry configuration `Telemetry.Enabled = false`
  When an agent executes turn loops or tool calls
  Then the telemetry tracer MUST execute a fast short-circuit check (`if (!enabled) return;`)
  And zero span objects or JSON payloads MUST be allocated or queued.

### Scenario 3: Dual-Tier JSONL Transcript Generation
  Given telemetry is enabled with storage type `Dual`
  When conversation turns complete
  Then `transcript.jsonl` MUST be updated with compact event records
  And `transcript_full.jsonl` MUST be updated with untruncated raw payloads
  And background worker MUST flush events to disk asynchronously.

### Scenario 4: OpenTelemetry W3C Trace Context Propagation
  Given an active session `SessionId = "sess-100"`
  When tool execution or sub-agent delegation occurs
  Then child spans MUST inherit `traceparent` from the parent turn span
  And OTLP exporter MUST export spans conforming to W3C Trace Context standards.
