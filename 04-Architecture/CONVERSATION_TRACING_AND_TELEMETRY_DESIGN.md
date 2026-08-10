# Conversation Tracing & Observability Architecture Design

## 🧭 Executive Summary
This document outlines the architectural design for a high-performance, non-blocking **Conversation Tracing & Observability System** for `Skight.AgentPlatform` in C# and F#.

The system benchmark draws inspiration from **Hermes Agent** (JSONL Trajectory logs) and **Claude Code** (OpenTelemetry hierarchical trace trees).

---

## 🔬 Benchmark Comparison Matrix

| Aspect | Hermes Agent | Claude Code | Skight Agent Platform (Proposed) |
| :--- | :--- | :--- | :--- |
| **Log Format** | `trajectory.jsonl` | `transcript.jsonl` + `transcript_full.jsonl` | Dual-Tier JSONL + OpenTelemetry OTLP |
| **Hierarchy** | Session -> Step | Session -> Turn -> LLM / Tool / Sub-Agent | W3C Hierarchical Trace Tree (`Session` -> `Turn` -> `LLM` / `Tool`) |
| **Telemetry Standard** | Custom JSON schema | OpenTelemetry TraceContext (`traceparent`) | OpenTelemetry + OpenInference AI Standards |
| **Performance Overhead** | Synchronous file append | Async event pipeline | Non-blocking `System.Threading.Channels` (Zero-Overhead when disabled) |
| **Visualization** | Custom CLI log viewer | Chrome DevTools / Jaeger | .NET Aspire Dashboard / Jaeger UI / Local Replay |

---

## 🏛️ Hierarchical Span Tree Model

```text
Root Session Span [agent.session] (SessionId, UserId)
  └── Turn 1 Span [agent.turn] (Iteration 1)
        ├── Context Compression Span [agent.compress] (Tokens, Ratio)
        ├── LLM Call Span [llm.call] (Model, Prompt Tokens, Completion Tokens)
        ├── Tool Execution Span [tool.execution] (ToolName: "file_write", DurationMs)
        └── Tool Execution Span [tool.execution] (ToolName: "terminal_execute", Status: Success)
```

---

## ⚡ High-Performance Zero-Overhead Architecture

1. **Configurable Toggle**:
   ```json
   {
     "Telemetry": {
       "Enabled": true,
       "StorageType": "Dual",
       "OtlpEndpoint": "http://localhost:4317"
     }
   }
   ```
2. **Zero Overhead when Disabled**:
   - Short-circuit `if (!enabled) return;` branch check.
3. **Non-Blocking In-Memory Channel**:
   - Core agent threads push `TelemetryEvent` to `System.Threading.Channels.Channel<TelemetryEvent>` in microseconds.
   - Background worker drains events and batches disk writes / OTLP exports.
