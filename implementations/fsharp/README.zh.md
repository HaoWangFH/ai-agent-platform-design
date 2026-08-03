# F# Agent 实现

> **映射至抽象工作流文档：** [CONVERSATION_LOOP_WORKFLOW.zh.md](../../docs/CONVERSATION_LOOP_WORKFLOW.zh.md)

## 概述

F# 实现使用纯函数式编程、不可变 Record 状态迁移、尾递归异步循环以及 Expecto 规范测试建模 4 阶段 Agent 对话循环工作流，根命名空间为 **`Skight.AgentPlatform.FSharp`**。

## 项目架构与目录结构

```
implementations/fsharp/
├── Skight.AgentPlatform.FSharp.sln
├── src/
│   └── Skight.AgentPlatform.FSharp/           （核心可执行库与应用）
│       ├── Skight.AgentPlatform.FSharp.fsproj
│       ├── Types.fs                             （领域类型与可辨识联合）
│       ├── ToolRegistry.fs                      （不可变且线程安全的工具注册表）
│       ├── Agent.fs                             （纯尾递归 Agent 循环管道）
│       └── Program.fs                           （支持 .env 与 Entra ID 认证的 CLI 入口）
└── tests/
    └── Skight.AgentPlatform.FSharp.Tests/     （Expecto 函数式规范测试）
        ├── Skight.AgentPlatform.FSharp.Tests.fsproj
        ├── AgentPipelineTests.fs                （纯管道步骤规范测试）
        ├── SequentialToolWorkflowSpec.fs        （多回合顺序工具调用规范测试）
        └── Main.fs                              （Expecto CLI 测试运行器入口）
```

## 运行测试 (Expecto 框架)

通过 Solution 文件运行 Expecto 测试套件：

```powershell
dotnet test implementations/fsharp/Skight.AgentPlatform.FSharp.sln
```

或使用详细的 CLI 标志直接运行 Expecto：

```powershell
dotnet run --project implementations/fsharp/tests/Skight.AgentPlatform.FSharp.Tests
```

## 函数式设计亮点

- **纯状态迁移**：`TurnState` 是不可变的。管道步骤返回 `StepResult<TurnState, TurnResult>`。
- **尾递归异步循环**：`runTurnLoop` 使用 F# 尾递归 (`return! runTurnLoop ...`) 在不增加栈深度的前提下执行任意多次工具迭代。
- **Expecto BDD 规范**：测试使用 Expecto `testList` 和 `testAsync` 树声明为一等函数值。
