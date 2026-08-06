# 任务实施清单：F# 智能体平台第二阶段

请使用此逐步清单指导第二阶段工具的实施，严格遵守 F# 智能体平台的函数式编程原则（不可变性、函数组合和判别联合）。

### 步骤 1：前置准备与子模块准备
- [ ] 确保 `main` 分支上的所有现有更改已在 CI 中通过测试。
- [ ] 在 `ai-agent-platform-design` 仓库中为 `feature/fsharp-phase2-tools` 创建新的特性分支。

### 步骤 2：实施 Git 自动化 (`GitTools.fs`)
- [ ] 在 `src/Skight.AgentPlatform.FSharp` 中创建 `GitTools.fs`（确保将其添加到 `ToolRegistry.fs` 之前的 `.fsproj` 中）。
- [ ] 定义包含纯函数的 `GitTool` 模块。
- [ ] 使用 `TerminalTool.executeCommand` 实施 `gitStatus ()`。
- [ ] 实施 `gitCommit (message: string)`。
- [ ] 实施 `gitPush ()`。
- [ ] 在 `tests/` 中添加单元测试，通过函数注入模拟终端输出（例如，将 `executeCommand` 作为依赖项传递）。
- [ ] 将 Git 函数组装到 `ToolRegistry.registerCoreTools` 中。

### 步骤 3：实施安全钩子 (`ApprovalGuard.fs`)
- [ ] 增强现有的 `ApprovalGuard.fs` 模块，通过函数组合拦截工具执行。
- [ ] 将危险正则表达式模式（例如 `rm -rf`, `del /s /q`）定义为活动模式 (Active Patterns) 或简单的判别函数。
- [ ] 修改工具执行管道以返回 `AgentState` 变体，如 `ApprovalRequired of string * CommandContext`。
- [ ] 更新 `Program.fs` 以对 `ApprovalRequired` 进行模式匹配，并在使用延续递归调用循环前提示用户 (Y/N)。

### 步骤 4：实施子智能体委派 (`DelegateTool.fs`)
- [ ] 创建 `DelegateTool.fs`。
- [ ] 实施 `delegateTask (role: string) (task: string)` 函数。
- [ ] 该函数应启动一个新的 `AgentSession` 记录（不可变状态），并将角色作为系统提示词进行种子设定。
- [ ] 调用 `AgentPipeline.runTurnLoop` 函数，传递隔离的状态和共享的 `ToolRegistry`。
- [ ] 确保在 `AgentSession` 记录中跟踪递归委派深度，以防止无限循环（最大深度限制为 1）。
- [ ] 将 `delegateTask` 组装到 `ToolRegistry.registerCoreTools` 中。

### 步骤 5：最终审查与集成
- [ ] 运行完整测试套件 (`dotnet test`)。
- [ ] 使用智能体递归调用 `gitCommit` 并推送其自身代码！
- [ ] 合并特性分支并更新主 `wiki` 仓库中的子模块指针。
