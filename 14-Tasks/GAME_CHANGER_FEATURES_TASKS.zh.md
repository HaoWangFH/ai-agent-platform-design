# 任务清单 Backlog：四大颠覆性 Agent 战略特性

> **目标实现：** `Skight.AgentPlatform` (C#) 与 `Skight.AgentPlatform.FSharp` (F#)  
> **状态：** 正在进行实现  
> **更新时间：** 2026-08-09

---

## 📌 任务 1：子 Agent 任务授权 (`delegate_task`) - 进行中
- [x] **1.1 DelegateTool 模块**：在 `DelegateTool.fs` 与 `DelegateTool.cs` 中实现基础功能。
- [x] **1.2 子 Agent Runner 绑定**：绑定具有独立 `AgentSessionState` 和上限迭代预算 (5) 的子 `runTurnAsync` 循环。
- [x] **1.3 批量并发**：在 F# 中利用 `Async.Parallel`、在 C# 中利用 `Task.WhenAll` 支持并行子 Agent 派生。
- [x] **1.4 BDD 规范测试**：实现 `DelegateToolSpecs.fs` (Expecto) 与 `DelegateToolTests.cs` (xUnit)。

---

## 📌 任务 2：代码质量止步门禁 (`pre_verify`)
- [ ] **2.1 文件变更追踪器**：在 `AgentSessionState` 中追踪 Dirty 文件变更状态。
- [ ] **2.2 拦截门禁**：如果文件已修改但未执行测试，拦截 `TurnOutcome.Completed`。
- [ ] **2.3 BDD 规范测试**：在 C# 和 F# 中实现验证门禁测试。

---

## 📌 任务 3：API 前置转向注入 (`/steer`)
- [ ] **3.1 转向队列**：在构建 API Payload 前清空待处理转向文本。
- [ ] **3.2 Tool 输出搭载**：将转向文本追加到最后一个 Tool 输出，维护角色交替规则。

---

## 📌 任务 4：长效临时向量记忆 (`memory_manager`)
- [ ] **4.1 持久化存储**：键值与向量 Embedding 记忆管理器。
- [ ] **4.2 临时注入**：在不污染会话数据库的前提下注入记忆上下文。
