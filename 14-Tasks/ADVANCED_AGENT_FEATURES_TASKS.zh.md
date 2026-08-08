# 优先级任务清单：Hermes & Claude Code 高级特性实现

> **目标平台：** `Skight.AgentPlatform` (C#) 与 `Skight.AgentPlatform.FSharp` (F#)  
> **状态：** 开放且已排定优先级  
> **更新时间：** 2026-08-08

---

## 📌 阶段 1：高优先级可靠性特性 (P0 - 建议优先领取)

### 任务 1：长度截断自动续写 (`finish_reason = "length"`)
- [ ] **1.1 领域签名更新**：在 `Types.fs` 与 `Types.cs` 的 `LlmTurnResponse` 中添加 `FinishReason`（如 `Stop`, `Length`, `ToolCalls`, `ContentFilter`）。
- [ ] **1.2 Pipeline 拦截器**：在 `AgentPipeline.fs` / `AgentPipeline.cs` 中实现 `handleLengthContinuation`。
- [ ] **1.3 续写提示词追加**：追加截断的部分响应以及 `"Your previous response was cut off due to max_tokens limit. Please continue..."` 提示词。
- [ ] **1.4 单元与规范测试**：编写 Expecto 和 xUnit 测试，验证长度截断自动续写逻辑（上限 4 次重试）。

### 任务 2：修改后验证门禁 (`pre_verify`)
- [ ] **2.1 Turn 状态 Dirty 标记**：在 `TurnState` 中追踪 `FilesModifiedInTurn: bool` 与 `VerificationExecutedInTurn: bool`。
- [ ] **2.2 拦截器门禁**：若 `FilesModifiedInTurn` 为 true 且 `VerificationExecutedInTurn` 为 false，拦截 `TurnOutcome.Completed`。
- [ ] **2.3 验证提示词注入**：注入提示词 `"You modified files during this turn. Please run tests or build verification commands to ensure your changes work cleanly."`。
- [ ] **2.4 规范测试**：编写 Expecto 和 xUnit 规范，验证修改文件后未运行测试会触发验证提示。

---

## 📌 阶段 2：消息与请求体稳健性 (P1 - 高优先级)

### 任务 3：消息序列与角色交替清洗器
- [ ] **3.1 清洗器模块**：实现 `MessageSequenceSanitizer.fs` / `MessageSequenceSanitizer.cs`。
- [ ] **3.2 孤立工具结果补全**：检测 assistant 消息中的工具调用是否缺少对应的 tool 响应；在后续 user/system 消息前注入合成工具错误消息。
- [ ] **3.3 请求前置序列化 Hook**：在调用 LLM API 之前用 `sanitizeMessageSequence` 包裹 `preparePayload`。
- [ ] **3.4 规范测试**：编写测试，确保损坏的消息序列不会导致 `400 Bad Request` 请求体错误。

### 任务 4：多 Provider LLM 故障转移链
- [ ] **4.1 故障转移装饰器**：实现包裹主 Caller 与备用 Caller 的 `FailoverLlmCaller`。
- [ ] **4.2 暂态错误触发**：在遭遇 `429 Rate Limit`、`500 Internal Error` 或 `ApiCallFailed` 时自动尝试备用模型。
- [ ] **4.3 集成测试**：创建 Mock 测试，演示主 Endpoint 失败时无缝切换到备用模型。

---

## 📌 阶段 3：高级控制与子 Agent 授权 (P2/P3 - 未来增强)

### 任务 5：动态工具掩码与上下文作用域 (P2)
- [ ] **5.1 上下文工具过滤器**：在 `ToolRegistry` 上暴露 `FilterToolsByPhase(phase)`。
- [ ] **5.2 只读作用域**：支持在仅研究阶段屏蔽文件/终端编辑工具。

### 任务 6：Token 级摘要压缩 (P2)
- [ ] **6.1 Token 估算器**：添加 Token 估算工具。
- [ ] **6.2 摘要 LLM 集成**：在接近上下文窗口限制时，实现基于 LLM 的历史对话摘要。

### 任务 7：带有隔离上下文的子 Agent 任务授权 (P3)
- [ ] **7.1 SubAgentDelegator**：实现允许 Agent 派生带有有限预算和干净历史记录的子 `runTurnAsync` 会话的工具。
- [ ] **7.2 父子状态同步**：将精简后的子 Agent 任务结果返回给主会话。
