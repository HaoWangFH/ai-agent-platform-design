# BDD 规范：对话链路追踪与可观测性系统 (Conversation Tracing Specs)

## 特性：对话链路追踪、Transcript 日志与 OpenTelemetry 导出

作为平台工程师与开发者，
我想追踪每一次对话轮次、LLM API 调用、工具执行与子 Agent 派生，
以便于我可以复盘对话、查看执行流、调试失败原因，并在功能关闭时实现零性能开销。

---

### 场景 1：启用状态下的非阻塞遥测记录
  假设遥测配置为 `Telemetry.Enabled = true`
  当 Agent 接收用户输入并执行 turn 循环时
  必须创建根会话 Span `agent.session` 与轮次 Span `agent.turn`
  且 LLM API 调用必须产生包含 Token 消耗指标的 `llm.call` Span
  且工具执行必须产生包含工具名称与耗时的 `tool.execution` Span
  且所有事件必须入队至内存 Channel，决不阻塞主执行线程。

### 场景 2：关闭状态下的零开销执行
  假设遥测配置为 `Telemetry.Enabled = false`
  当 Agent 执行 turn 循环或工具调用时
  遥测追踪器必须执行极速短路检查 (`if (!enabled) return;`)
  且不得分配或入队任何 Span 对象或 JSON Payload。

### 场景 3：双层 JSONL 轨迹文件生成
  假设遥测已开启且存储模式为 `Dual`
  当对话轮次完成时
  `transcript.jsonl` 必须更新为精简版事件记录
  且 `transcript_full.jsonl` 必须更新为未截断的 Raw Payload 记录
  且后台 Worker 必须异步将事件刷新至磁盘。

### 场景 4：OpenTelemetry W3C 上下文链路传播
  假设当前活动会话 `SessionId = "sess-100"`
  当发生工具执行或子 Agent 派生时
  子 Span 必须从父级 turn Span 继承 `traceparent`
  且 OTLP 导出器必须导出符合 W3C Trace Context 标准的 Span。
