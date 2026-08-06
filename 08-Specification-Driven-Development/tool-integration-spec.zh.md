# 规范文档：C# 与 F# 智能体平台的工具集成规范

## 1. 简介
本规范定义了 C# (面向对象) 与 F# (函数式) 实现中第二阶段工具的行为和验收标准。

## 2. 需求与 BDD 行为规范

### 2.1 Git 自动化工具 (`git_status`, `git_commit`, `git_push`)
- **场景**：自动暂存并提交工作区更改。
  - **假设 (Given)** 智能体已修改 `workspaceRoot` 内的文件。
  - **当 (When)** 智能体使用消息 `"Fix issue"` 调用 `git_commit`。
  - **那么 (Then)** `workspaceRoot` 中的所有修改文件均被暂存并提交。
  - **并且 (And)** 工具返回包含提交 Hash 的成功字符串。

### 2.2 子智能体委派工具 (`delegate_task`)
- **场景**：将复杂子任务委派给独立的子智能体。
  - **假设 (Given)** 主智能体收到需要多步调查的任务。
  - **当 (When)** 主智能体调用 `delegate_task`，传入 `role="Researcher"` 和 `task="Summarize README"`。
  - **那么 (Then)** 生成具有独立消息历史记录的子智能体会话。
  - **并且 (And)** 子智能体在深度限制 1 内运行，然后将其最终答案返回给父智能体。

### 2.3 安全护栏 (`execute_command` 拦截)
- **场景**：在执行前拦截破坏性命令。
  - **假设 (Given)** 智能体收到递归删除文件的请求（如 `rm -rf /` 或 `del /s /q *`）。
  - **当 (When)** 使用危险负载调用 `execute_command`。
  - **那么 (Then)** 在调用 Shell 进程前暂停执行。
  - **并且 (And)** 向用户界面呈现审批提示。

---

## 3. 对等性验证标准
- [ ] C# `GitTools.cs` 通过 XUnit / MSpec BDD 规范测试。
- [ ] F# `GitTools.fs` 通过 Expecto BDD 规范测试。
- [ ] C# 和 F# 的委派实现均能防止无限子智能体嵌套（深度 <= 1）。
