# C# Agent 实现

> **映射至抽象工作流文档：** [CONVERSATION_LOOP_WORKFLOW.zh.md](../../docs/CONVERSATION_LOOP_WORKFLOW.zh.md)

## 概述

C# 实现使用 .NET 8、`Azure.AI.OpenAI` SDK 以及强类型的 `TurnResult` 对象建模 4 阶段 Agent 对话循环工作流，根命名空间为 **`Skight.AgentPlatform`**。

## 项目架构与目录结构

```
implementations/csharp/
├── Skight.AgentPlatform.sln
├── src/
│   └── Skight.AgentPlatform/            （核心可执行应用）
│       ├── Skight.AgentPlatform.csproj
│       ├── Agent.cs                      （4 阶段对话循环与状态机）
│       ├── ToolRegistry.cs               （工具注册与异步执行运行时）
│       ├── Tools.cs                      （Mock 工具定义）
│       └── Program.cs                    （支持 .env 与 Entra ID 认证的 CLI 入口）
└── tests/
    ├── Skight.AgentPlatform.Tests/      （xUnit + FluentAssertions 单元与规范测试）
    ├── Skight.AgentPlatform.MSpec.Tests/  （Machine.Specifications BDD 上下文规范测试）
    └── Skight.AgentPlatform.LightBDD.Tests/（LightBDD 代码优先场景 BDD 测试）
```

## 运行测试

通过 Solution 文件一次性运行全部 3 个规范测试套件：

```powershell
dotnet test implementations/csharp/Skight.AgentPlatform.sln
```

或单独运行特定的测试框架：
- **xUnit**：`dotnet test implementations/csharp/tests/Skight.AgentPlatform.Tests`
- **MSpec**：`dotnet test implementations/csharp/tests/Skight.AgentPlatform.MSpec.Tests`
- **LightBDD**：`dotnet test implementations/csharp/tests/Skight.AgentPlatform.LightBDD.Tests`

## 工作流映射

### 1. 阶段 1：回合前言 (Turn Prologue)
- 在 `Agent.RunAsync(string userInput): Task<TurnResult>` 中执行。
- 将 `ChatRequestUserMessage` 追加至 `_messages`。
- 重置每回合状态：`apiCalls = 0`、`_interruptRequested = false`、`emptyContentRetries = 0`。

### 2. 阶段 2：主对话循环 (Main Conversation Loop)
- **2.1 API 前检查：** 在 `while (apiCalls < MaxIterations)` 中检查。检查 `_interruptRequested` 和 `MaxIterations`。
- **2.2 消息准备：** `PrepareApiMessages()` 浅拷贝 `_messages`。
- **2.3 上下文窗口保护：** 当消息数量 > `ContextWindowLimit` 时，`CompressContextIfNeeded()` 裁剪中间历史。
- **2.4 内部重试循环：** 带退避的重试循环 `await Task.Delay((int)Math.Pow(2, retry) * 1000)`。
- **2.5 响应规范化：** 访问 `completions.Choices[0].Message`。
- **2.6 工具执行路径：**
  - 根据已注册工具名称验证工具 Schema（未注册工具自我纠正）。
  - 使用 `JsonDocument.Parse` 验证 JSON 参数。
  - 带 `try...catch` 运行时错误处理执行工具。
  - 将带有 `ToolCalls` 的助手消息插入到工具响应消息之前，并继续循环。
- **2.7 最终文本响应路径：**
  - 在返回备用文本 `"(empty response)"` 之前使用提示推动重试空文本响应。
  - 返回 `Completed = true, ExitReason = "text_response"` 的 `TurnResult`。

### 3. 阶段 3 与 4：回合终结 (Turn Finalization)
- 返回结构化的 `TurnResult` 对象。
