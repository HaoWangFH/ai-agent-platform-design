# 任务实施清单：C# 智能体平台第二阶段

请使用此逐步清单指导架构与规范文档中概述的第二阶段工具的实施。

### 步骤 1：前置准备与子模块准备
- [ ] 确保 `main` 分支上的所有现有更改已在 CI 中通过测试。
- [ ] 在 `ai-agent-platform-design` 仓库中为 `feature/phase2-tools` 创建新的特性分支。

### 步骤 2：实施 Git 自动化 (`GitTools`)
- [ ] 在 `src/Skight.AgentPlatform` 中创建 `GitTools.cs`。
- [ ] 使用 `TerminalTool.ExecuteCommandAsync` 实施 `git_status()`。
- [ ] 实施 `git_commit(string message)`。
- [ ] 实施 `git_push()`。
- [ ] 在 `tests/Skight.AgentPlatform.Tests` 中添加模拟终端输出的单元测试。
- [ ] 将 `GitTools` 组装到 `Tools.RegisterCoreTools` 中。

### 步骤 3：实施安全钩子 (`ApprovalGuard`)
- [ ] 增强现有的 `ApprovalGuard.cs` 以拦截 `ToolRegistry.ExecuteToolAsync`。
- [ ] 定义危险正则表达式模式数组（例如 `rm -rf`, `del /s /q`）。
- [ ] 如果检测到匹配，暂停轮次并返回 `ApprovalRequired` 状态。
- [ ] 修改 `Program.cs` 以在需要审批时提示用户 (Y/N)，然后再恢复执行。

### 步骤 4：实施子智能体委派 (`DelegateTool`)
- [ ] 创建 `DelegateTool.cs`。
- [ ] 实施 `delegate_task(string role, string task)` 以启动一个新的独立 `AgentRunner`。
- [ ] 确保子智能体使用相同的 `ToolRegistry` 但拥有独立的 `AgentSessionState`。
- [ ] 测试递归子智能体启动限制（最大深度限制为 1）。
- [ ] 将 `DelegateTool` 组装到 `Tools.RegisterCoreTools` 中。

### 步骤 5：最终审查与集成
- [ ] 运行完整测试套件 (`dotnet test`)。
- [ ] 使用智能体递归调用 `git_commit` 并推送其自身代码！
- [ ] 合并特性分支并更新主 `wiki` 仓库中的子模块指针。
