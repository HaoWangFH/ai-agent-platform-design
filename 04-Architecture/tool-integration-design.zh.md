# 设计文档：双范式工具集成架构 (C# 与 F#)

## 1. 概述
本文档定义了将第二阶段核心与扩展工具（子智能体委派、Git 自动化和安全护栏）集成到 AI 智能体平台中的架构设计。为保证语言对等性（Language Parity），本设计同时兼容 **面向对象 (C#)** 与 **函数式 (F#)** 两种编程范式。

---

## 2. 编程范式与设计模式

| 功能组件 | C# (面向对象) 模式 | F# (函数式) 模式 |
| :--- | :--- | :--- |
| **工具抽象** | 静态工具类 / 接口 | 纯模块与纯函数 (`let` 绑定) |
| **状态管理** | 可变的 `AgentSessionState` 类 | 不可变的 `AgentSession` 记录 (Record) |
| **子智能体委派** | 中介者/监督者模式 (`DelegateTool.cs`) | 函数组合与递归 Loop 调用 (`DelegateTool.fs`) |
| **安全拦截器** | `ToolRegistry` 上的装饰器模式 (`ApprovalGuard.cs`) | 活动模式 (Active Patterns) / Result 类型封装 (`ApprovalGuard.fs`) |
| **Git 操作** | Shell 进程运行器的外观模式 (Facade) | 接受执行委托的纯函数封装 |

---

## 3. 组件设计

### 3.1 工具注册表 (`ToolRegistry`)
- **C#**：`ToolRegistry` 存储与 OpenAPI/JSON Schema 关联的 `Func<string, Task<string>>` 处理程序。
- **F#**：`ToolRegistry` 是包装工具名称到处理函数 `string -> Async<string>` 映射的记录或模块。

### 3.2 子智能体委派 (`delegate_task`)
- **C#**：实例化带有自定义系统提示词的 `AgentRunner`，执行 `RunTurnLoopAsync`，返回最终字符串响应。
- **F#**：获取当前上下文，构建独立的 `AgentSession` 记录，调用 `runTurnLoop`，异步提取最终消息。

### 3.3 安全与审批护栏 (`ApprovalGuard`)
- **C#**：在执行前拦截 `execute_command`。如果匹配到危险模式，触发用户审批回调。
- **F#**：通过活动模式 `(|DangerousCommand|_|)` 评估命令。如果匹配，产生 `ApprovalRequired` 联合分支。

---

## 4. 对等性与文件结构

```
implementations/
├── csharp/src/Skight.AgentPlatform/
│   ├── GitTools.cs
│   ├── DelegateTool.cs
│   └── ApprovalGuard.cs
└── fsharp/src/Skight.AgentPlatform.FSharp/
    ├── GitTools.fs
    ├── DelegateTool.fs
    └── ApprovalGuard.fs
```
