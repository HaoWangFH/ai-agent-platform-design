# F# Agent 实现指南

> **映射至抽象工作流规范：** [CONVERSATION_LOOP_WORKFLOW.zh.md](../../docs/CONVERSATION_LOOP_WORKFLOW.zh.md)

## 概述

F# 实现最大程度地利用了 **函数式编程范式（Functional Programming）**，为 4 阶段 Agent 会话循环工作流构建了高度可组合、灵活且纯粹（Pure）的架构。

## 核心函数式架构亮点

1. **纯粹可变状态管道 (`TurnState`)**
   - 整个回合状态（`Messages`、`ApiCalls`、`EmptyContentRetries`、`InterruptRequested`、`Config`）作为一个不可变记录传递。回合循环执行期间不修改任何 `mutable` 变量或状态标志。
2. **尾递归异步循环 (`AgentPipeline.runTurnLoop`)**
   - 将主循环实现为纯粹的尾递归 async 函数，消除了过程化的 `while` 循环和可变状态修改。
3. **步骤管道组合 (`|>`)**
   - 消息载荷准备和上下文压缩使用 F# 前向管道运算符进行组合：
     ```fsharp
     let preparedPayload = 
         state.Messages 
         |> prepareApiMessages 
         |> compressContextIfNeeded state.Config.ContextWindowLimit
     ```
4. **单子步骤结果控制流 (`StepResult<'State, 'Result>`)**
   - 干净的可区分联合 (`Continue 'State | Exit 'Result`)，用于预检查与退出条件的函数式表达。
5. **一等函数组合与偏应用 (`LlmCaller` 与 `ToolExecutor`)**
   - LLM 调用者和工具执行逻辑被定义为一等、可组合的函数类型：
     ```fsharp
     type LlmCaller = FunctionDefinition list -> ChatRequestMessage list -> Async<Result<ChatCompletions, string>>
     type ToolExecutor = string -> string -> Async<string>
     ```
   - 支持偏函数应用（Partial Application）、可组合中间件（日志、速率限制、指标追踪）以及无需 Mock 框架的依赖注入单元测试。

## 文件结构

- `Types.fs`: 领域类型（`ExitReason`、`TurnResult`、`TurnState`、`StepResult`、`LlmCaller`、`ToolExecutor`）。
- `ToolRegistry.fs`: 暴露可组合 `AsExecutor: ToolExecutor` 的函数式工具注册表。
- `Agent.fs`: 包含纯函数步骤与尾递归循环的 `AgentPipeline` 模块，外层由 `Agent` 类进行包装。
- `Program.fs`: 注册了 F# Mock 工具的控制台 CLI 入口点。
- `AgentPlatformFSharp.fsproj`: 目标框架为 .NET 8.0 的 F# 项目文件。

## 工作流映射

### 1. 阶段 1：回合序言 (Turn Prologue)
- 在 `agent.RunAsync(userInput: string) : Async<TurnResult>` 中执行。
- 将 `ChatRequestUserMessage` 追加到规范化消息中。
- 构造初始的不可变 `TurnState`。

### 2. 阶段 2：主会话循环 (Main Conversation Loop)
- **2.1 API 前检查：** 可组合步骤函数 `checkInterrupt` 与 `checkBudget` 返回 `StepResult`。
- **2.2 & 2.3 消息准备与上下文窗口保护：** 通过前向管道运算符 (`|>`) 组合。
- **2.4 内部重试循环：** 使用递归 F# 异步函数 `callLlmWithRetry` 实现。
- **2.5 响应分类：** 使用 `match apiResult with | Ok completions -> ... | Error err -> ...` 进行模式匹配。
- **2.6 工具执行路径：** `processToolCalls` 使用 `Async.Parallel` 并行异步执行工具调用，并返回更新后的不可变状态。
- **2.7 最终文本响应路径：** `processTextResponse` 使用模式匹配处理空文本内容与提示词推动重试。

### 3. 阶段 3 & 4：回合终结 (Turn Finalization)
- 返回不可变的 F# `TurnResult` 记录。
