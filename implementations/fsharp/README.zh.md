# F# Agent 实现指南

> **映射至抽象工作流规范：** [CONVERSATION_LOOP_WORKFLOW.zh.md](../../docs/CONVERSATION_LOOP_WORKFLOW.zh.md)

## 概述

F# 实现采用了 F# 的函数式编程范式建模 4 阶段 Agent 会话循环工作流：**可区分联合 (Discriminated Unions)**、**记录类型 (Record Types)**、**模式匹配 (Pattern Matching)**、**异步工作流 (Async Workflows)** 以及 **前向管道运算符 (`|>`)**。

## 文件结构

- `Types.fs`: 代数数据类型（`ExitReason` 可区分联合、`TurnResult` 记录类型、`AgentConfig` 记录类型）。
- `ToolRegistry.fs`: 支持 F# `Async<string>` 工具处理器的函数式工具注册表。
- `Agent.fs`: 利用前向管道和递归重试的函数式 Agent 工作流实现。
- `Program.fs`: 注册了 F# Mock 工具的控制台 CLI 入口点。
- `AgentPlatformFSharp.fsproj`: 目标框架为 .NET 8.0 的 F# 项目文件。

## 工作流映射

### 1. 阶段 1：回合序言 (Turn Prologue)
- 在 `agent.RunAsync(userInput: string) : Async<TurnResult>` 中执行。
- 将 `ChatRequestUserMessage` 追加到 `messages`。
- 重置每回合状态（`apiCalls`、`interruptRequested`、`emptyContentRetries`）。

### 2. 阶段 2：主会话循环 (Main Conversation Loop)
- **2.1 API 前检查：** 在 `while apiCalls < config.MaxIterations && turnResult.IsNone do` 开头检查 `interruptRequested` 和 `config.MaxIterations`。
- **2.2 & 2.3 消息准备与上下文窗口保护：** 使用前向管道运算符 (`|>`) 进行干净的数据转换：
  ```fsharp
  let preparedMessages = 
      messages 
      |> self.PrepareApiMessages 
      |> self.CompressContextIfNeeded
  ```
- **2.4 内部重试循环：** 使用带 `do! Async.Sleep delayMs` 的递归 F# 异步函数 `ExecuteApiWithRetry(messages, retryCount)` 实现。
- **2.5 响应分类：** 使用 `match apiResult with | Ok completions -> ... | Error err -> ...` 进行模式匹配。
- **2.6 工具执行路径：**
  - 评估已注册工具名称集合（`registeredNames.Contains(name)`）。
  - 在 F# `try...with` 块中安全处理 JSON 解析。
  - 异步执行工具处理器并构建工具结果消息。
  - 追加工具消息并继续 `while` 循环。
- **2.7 最终文本响应路径：**
  - 使用模式匹配处理空文本内容，在设置后备文本 `"(empty response)"` 前进行提示词推动重试。
  - 返回带有 `Completed = true, ExitReason = TextResponse finalText` 的 `TurnResult`。

### 3. 阶段 3 & 4：回合终结 (Turn Finalization)
- 返回不可变的 F# `TurnResult` 记录类型。
