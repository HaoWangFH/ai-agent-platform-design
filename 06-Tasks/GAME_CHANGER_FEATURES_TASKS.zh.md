# 全景任务清单 Backlog：AI Agent 平台特性指南

> **目标实现：** `Skight.AgentPlatform` (C#) 与 `Skight.AgentPlatform.FSharp` (F#)  
> **状态：** 正在进行实现  
> **更新时间：** 2026-08-10

---

## 📌 第一阶段：四大核心颠覆性特性 (当前主攻)

### 任务 1：子 Agent 任务授权 (`delegate_task`) - 【已完成】
- [x] **1.1 DelegateTool 模块**：在 `DelegateTool.fs` 与 `DelegateTool.cs` 中实现基础功能。
- [x] **1.2 子 Agent Runner 绑定**：绑定具有独立 `AgentSessionState` 和上限迭代预算 (5) 的子 `runTurnAsync` 循环。
- [x] **1.3 批量并发**：在 F# 中利用 `Async.Parallel`、在 C# 中利用 `Task.WhenAll` 支持并行子 Agent 派生。
- [x] **1.4 BDD 规范测试**：实现了 `DelegateToolSpecs.fs` (Expecto) 与 `DelegateToolTests.cs` (xUnit)。全部测试通过！

### 任务 2：云原生向量记忆架构 (`IMemoryStore`) - 【已完成】
- [x] **2.1 统一记忆接口**：在 C# 中创建 `IMemoryStore`，在 F# 中创建 `MemoryStore.fs`。
- [x] **2.2 SQLite 嵌入式适配器**：实现具备 `UserId` 多租户隔离的 `SqliteMemoryStore`。
- [x] **2.3 单元与 BDD 测试套件**：添加 `MemoryStoreSpecs.fs` (Expecto) 与 `MemoryStoreTests.cs` (xUnit)。全部测试通过！

### 任务 3：代码质量止步门禁 (`pre_verify`) - 【已完成】
- [x] **3.1 文件变更追踪器**：在 `AgentSessionState` 与 `TurnState` 中追踪 Dirty 文件变更状态。
- [x] **3.2 拦截门禁**：如果文件已修改但未执行测试，拦截 `TurnOutcome.Completed`（最大 Nudge 尝试次数 = 2）。
- [x] **3.3 单元与 BDD 测试套件**：添加 `PreVerifySpecs.fs` (Expecto) 与 `PreVerifyTests.cs` (xUnit)。全部测试通过！

### 任务 4：API 前置转向注入 (`/steer`) - 【已完成】
- [x] **4.1 转向队列**：在构建 API Payload 前清空待处理转向文本 (`ConcurrentQueue<string>`)。
- [x] **4.2 Tool 输出搭载**：将转向文本追加到最后一个 Tool 输出，维护角色交替规则 (`user -> assistant -> tool -> user`)。
- [x] **4.3 单元与 BDD 测试套件**：添加 `SteeringSpecs.fs` (Expecto) 与 `SteeringTests.cs` (xUnit)。全部测试通过！

---

## 📌 第二阶段：高级企业级生态扩展特性 (演进路线图)

### 任务 5：上下文自动压缩引擎 (`context_compressor`) - 【已完成】
- [x] **5.1 Token 预算监控**：自动检测对话长度是否超出 `ContextWindowLimit` 的 80% 阈值。
- [x] **5.2 轮次总结剪枝**：将早期对话历史压缩为 `TurnSummary` 系统消息，同时保留 System Prompt 和近期上下文。
- [x] **5.3 单元与 BDD 测试套件**：添加 `ContextCompressorSpecs.fs` (Expecto) 与 `ContextCompressorTests.cs` (xUnit)。全部测试通过！

### 任务 6：交互式对齐网关 (`clarify_tool`) - 【已完成】
- [x] **6.1 结构化问答工具**：实现 `clarify_tool` Schema 以支持交互式选择题弹窗 (`question`, `options`, `is_multi_select`)。
- [x] **6.2 无人值守降级**：实现非交互/无头模式下的自动默认选项降级逻辑。
- [x] **6.3 单元与 BDD 测试套件**：添加 `ClarifyToolSpecs.fs` (Expecto) 与 `ClarifyToolTests.cs` (xUnit)。全部测试通过！

### Task 7: Background Cron & Scheduler (`cronjob_tools`)
- [ ] **7.1 One-Shot & Cron Timers**: Background task scheduler with reactive agent wakeups.
