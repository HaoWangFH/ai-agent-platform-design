# TypeScript Agent 实现指南

> **映射至抽象工作流规范：** [CONVERSATION_LOOP_WORKFLOW.zh.md](../../docs/CONVERSATION_LOOP_WORKFLOW.zh.md)

## 概述

TypeScript 实现采用了 async/await、强类型 TypeScript 接口以及官方 `openai` NPM 包建模 4 阶段 Agent 会话循环工作流。

## 文件结构

- `src/Agent.ts`: `Agent` 类、`TurnResult` 接口和 `AgentConfig` 接口。
- `src/ToolRegistry.ts`: 包含 JSON Schema 的 TypeScript 工具注册与执行表 `ToolRegistry`。
- `src/tools.ts`: 工具实现。
- `src/index.ts`: 基于 Readline 的交互式 CLI 循环。

## 工作流映射

### 1. 阶段 1：回合序言 (Turn Prologue)
- 在 `Agent.run(userInput: string): Promise<TurnResult>` 中实现。
- 将用户提示词追加到 `this.messages`。
- 重置每回合变量：`apiCalls = 0`、`this.interruptRequested = false`、`emptyContentRetries = 0`。

### 2. 阶段 2：主会话循环 (Main Conversation Loop)
- **2.1 API 前检查：** 在 `while (apiCalls < this.maxIterations)` 开头检查 `this.interruptRequested` 和 `maxIterations`。
- **2.2 消息准备：** `prepareApiMessages()` 浅拷贝 `this.messages`。
- **2.3 上下文窗口保护：** 当消息数量 > `contextWindowLimit` 时，`compressContextIfNeeded()` 裁剪中间历史。
- **2.4 内部重试循环：** 带 `await new Promise(...)` 指数延迟的异步重试循环。
- **2.5 响应规范化：** 提取 `response.choices[0].message`。
- **2.6 工具执行路径：**
  - 使用诊断工具结果自动纠正未注册的工具调用。
  - 使用 `JSON.parse` 安全解析 JSON 参数。
  - 在 `try...catch` 中包裹工具执行。
  - 追加工具结果（`role: 'tool'`）并继续循环。
- **2.7 最终文本响应路径：**
  - 在返回后备文本前，通过提示词推动处理空文本响应。
  - 返回带有 `completed: true, exitReason: 'text_response'` 的 `TurnResult`。

### 3. 阶段 3 & 4：回合终结 (Turn Finalization)
- 返回结构化的 `TurnResult` 对象。
