# C# Agent 实现指南

> **映射至抽象工作流规范：** [CONVERSATION_LOOP_WORKFLOW.zh.md](../../docs/CONVERSATION_LOOP_WORKFLOW.zh.md)

## 概述

C# 实现使用了 .NET 8、`Azure.AI.OpenAI` SDK 以及强类型的 `TurnResult` 对象建模 4 阶段 Agent 会话循环工作流。

## 文件结构

- `Agent.cs`: 实现 4 阶段循环的 `Agent` 类与 `TurnResult` 类。
- `ToolRegistry.cs`: 支持异步工具执行的线程安全工具注册表。
- `Tools.cs`: 注册的 Mock 工具。
- `Program.cs`: 支持 Azure OpenAI 和标准 OpenAI 端点的 CLI 入口点。

## 工作流映射

### 1. 阶段 1：回合序言 (Turn Prologue)
- 在 `Agent.RunAsync(string userInput): Task<TurnResult>` 中执行。
- 将 `ChatRequestUserMessage` 追加到 `_messages`。
- 重置每回合状态：`apiCalls = 0`、`_interruptRequested = false`、`emptyContentRetries = 0`。

### 2. 阶段 2：主会话循环 (Main Conversation Loop)
- **2.1 API 前检查：** 在 `while (apiCalls < MaxIterations)` 开头检查 `_interruptRequested` 和 `MaxIterations`。
- **2.2 消息准备：** `PrepareApiMessages()` 浅拷贝 `_messages`。
- **2.3 上下文窗口保护：** 当消息数量 > `ContextWindowLimit` 时，`CompressContextIfNeeded()` 裁剪中间历史。
- **2.4 内部重试循环：** 带 `await Task.Delay((int)Math.Pow(2, retry) * 1000)` 的重试循环。
- **2.5 响应规范化：** 访问 `completions.Choices[0].Message`。
- **2.6 工具执行路径：**
  - 对照已注册工具名称验证工具 Schema（未注册工具自我纠正）。
  - 使用 `JsonDocument.Parse` 验证 JSON。
  - 带 `try...catch` 运行时错误处理的工具执行。
  - 在工具响应消息之前插入带有 `ToolCalls` 的 assistant 消息并继续循环。
- **2.7 最终文本响应路径：**
  - 在返回后备文本 `"(empty response)"` 之前用提示词推动重试空文本响应。
  - 返回带有 `Completed = true, ExitReason = "text_response"` 的 `TurnResult`。

### 3. 阶段 3 & 4：回合终结 (Turn Finalization)
- 返回结构化的 `TurnResult` 对象。
