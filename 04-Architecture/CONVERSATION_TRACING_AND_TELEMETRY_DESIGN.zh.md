# 对话链路追踪与可观测性系统架构设计 (Conversation Tracing & Observability Design)

## 🧭 执行摘要
本文档概述了 `Skight.AgentPlatform` (C# & F#) 高性能、非阻塞 **对话链路追踪与可观测性系统** 的架构设计。

本系统深度借鉴了 **Hermes Agent** (JSONL 轨迹日志) 与 **Claude Code** (OpenTelemetry 树状 Span 链路追踪) 的优秀设计。

---

## 🔬 标杆对比矩阵 (Benchmark Matrix)

| 维度 | Hermes Agent 机制 | Claude Code 机制 | Skight Agent 平台拟议设计 |
| :--- | :--- | :--- | :--- |
| **日志存储格式** | `trajectory.jsonl` | `transcript.jsonl` + `transcript_full.jsonl` | 双层 JSONL 轨迹 + OpenTelemetry OTLP |
| **层级结构** | Session -> Step | Session -> Turn -> LLM / Tool / Sub-Agent | W3C 树状 Trace 结构 (`Session` -> `Turn` -> `LLM` / `Tool`) |
| **遥测标准** | 自定义 JSON Schema | OpenTelemetry TraceContext (`traceparent`) | OpenTelemetry + OpenInference AI 行业标准 |
| **性能开销** | 同步文件追加 | 异步事件管道 | 非阻塞 `System.Threading.Channels` (关闭时零开销) |
| **可视化与复盘** | 自定义 CLI 查看器 | Chrome DevTools / Jaeger | .NET Aspire Dashboard / Jaeger UI / 本地 HTML 视盘 |

---

## 🏛️ 树状 Trace 链路模型 (Hierarchical Span Tree)

```text
Root Session Span [agent.session] (SessionId, UserId)
  └── Turn 1 Span [agent.turn] (轮次 1)
        ├── 上下文压缩 Span [agent.compress] (Token 消耗, 压缩率)
        ├── LLM 调用 Span [llm.call] (Model, Prompt Tokens, Completion Tokens)
        ├── 工具执行 Span [tool.execution] (ToolName: "file_write", 耗时 Ms)
        └── 工具执行 Span [tool.execution] (ToolName: "terminal_execute", 状态: 成功)
```

---

## ⚡ 高性能零开销架构 (Zero-Overhead Architecture)

1. **开关配置**:
   ```json
   {
     "Telemetry": {
       "Enabled": true,
       "StorageType": "Dual",
       "OtlpEndpoint": "http://localhost:4317"
     }
   }
   ```
2. **关闭时零开销**:
   - 当 `Enabled = false` 时，执行 `if (!enabled) return;` 极速短路分支，开销为纳秒级。
3. **内存通道异步非阻塞**:
   - 核心 Agent 执行线程将 `TelemetryEvent` 写入内存队列 (`System.Threading.Channels.Channel`)，微秒级完成。
   - 后台 Worker 线程异步批量写入磁盘 JSONL 或导出 OTLP，决不阻塞 LLM 响应与工具执行。
