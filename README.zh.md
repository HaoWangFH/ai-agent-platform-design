# AI Agent 平台设计指南 (AI Agent Platform Design)

本仓库包含轻量级、企业级 AI Agent 平台 (`Skight.AgentPlatform` C# 与 `Skight.AgentPlatform.FSharp` F#) 的架构分析、抽象对话循环规范、功能对齐进度追踪以及双语言平行实现。

## 🔄 SDLC 全生命周期目录结构 / Sitemap

本项目遵循严格的 7 阶段 **软件开发生命周期 (SDLC)**，并采用按序编号的目录前缀：

| SDLC 阶段 | 阶段定位 | 专属目录 | 核心职责与产出物 |
| :--- | :--- | :--- | :--- |
| **阶段 1** | **需求与竞品分析 (Analyze)** | [`03-Analysis/`](03-Analysis/) | 标杆框架差距分析 ([`HERMES_CLAUDE_CODE_ANALYSIS.zh.md`](03-Analysis/HERMES_CLAUDE_CODE_ANALYSIS.zh.md))、对齐报告 ([`HERMES_PARITY_REPORT.zh.md`](03-Analysis/HERMES_PARITY_REPORT.zh.md))、循环边界研究 ([`HERMES_LOOP_CONDITIONS.zh.md`](03-Analysis/HERMES_LOOP_CONDITIONS.zh.md))。 |
| **阶段 2** | **架构设计 (Design)** | [`04-Architecture/`](04-Architecture/) | 系统架构、五层对话循环设计 ([`ITERATION_LOOP_DESIGN.zh.md`](04-Architecture/ITERATION_LOOP_DESIGN.zh.md))、多轮工作流 ([`CONVERSATION_LOOP_WORKFLOW.zh.md`](04-Architecture/CONVERSATION_LOOP_WORKFLOW.zh.md))。 |
| **阶段 3** | **测试规范 (Specification)** | [`05-Specification-Driven-Development/`](05-Specification-Driven-Development/) | BDD 规范说明 ([`GAME_CHANGER_FEATURES_SPECS.zh.md`](05-Specification-Driven-Development/GAME_CHANGER_FEATURES_SPECS.zh.md))。 |
| **阶段 4** | **规划与 Backlog (Planning)** | [`06-Tasks/`](06-Tasks/) | Master 任务 Backlog 与演进进度 ([`GAME_CHANGER_FEATURES_TASKS.zh.md`](06-Tasks/GAME_CHANGER_FEATURES_TASKS.zh.md))。 |
| **阶段 5** | **代码实施 (Implement)** | [`implementations/`](implementations/) | C# 核心库与 WebAPI/gRPC 服务封装 (`csharp/`)、F# 核心库与 Expecto 测试套件 (`fsharp/`)。 |
| **阶段 6** | **验证与测试 (Verify)** | [`07-Testing/`](07-Testing/) | 自动化测试套件 (Expecto 60/60, xUnit 20/20) 与代码质量止步门禁 (`pre_verify`)。 |
| **阶段 7** | **知识沉淀 (Knowledge)** | [`08-Knowledge-System/`](08-Knowledge-System/) | 领域术语词典与业务知识沉淀 ([`KNOWLEDGE_INDEX.zh.md`](08-Knowledge-System/KNOWLEDGE_INDEX.zh.md))。 |

### 🛠️ 辅助工程资源目录
- **`00-Getting-Started/`**：开发者环境配置与入门指南。
- **`01-Engineering-Workflow/`**：SDLC 全生命周期工程方法论。
- **`02-AI-Workspace/`**：AI 助手工作区规则与配置。
- **`09-Standards/`**：编码规范、F# 函数式迁移指南与 Git Commit 规范。
- **`10-Skill-Library/` & `11-Prompt-Library/`**：可复用 Agent 技能与 System Prompt 库。
- **`12-Templates/` & `13-Automation/`**：项目样板与 CI/CD 自动化。
- **`14-Agent-Framework/`**：提取的 Agent 接口与基础设施规范。
- **`15-Cloud-Infrastructure/`**：云基础设施、Kubernetes、Docker 与服务部署配置。

## 🎯 核心价值
通过将 Agent 核心逻辑抽象并在 C# (.NET 10) 与 F# 中进行双语言平行实现，我们为多租户、云原生的企业级 AI Agent 服务端部署提供了坚实的基础与严格的 BDD 规范验证。
