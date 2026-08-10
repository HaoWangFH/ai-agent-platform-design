# AI Agent 平台工程工作流与 SDLC 全生命周期指南

本文档概述了 AI Agent 平台 (`Skight.AgentPlatform`) 如何结构化映射与追踪 **软件开发生命周期 (SDLC)** 的各个阶段。

---

## 🔄 SDLC 开发生命周期阶段映射

| 阶段 | SDLC 开发环节 | 专属目录 | 核心职责与产出物 |
| :--- | :--- | :--- | :--- |
| **阶段 1：分析 (Analyze)** | 需求分析与竞品差距分析 | [`03-Analysis/`](../03-Analysis/) | 标杆框架对比分析 (`HERMES_CLAUDE_CODE_ANALYSIS.md`)、功能差距报告 (`HERMES_PARITY_REPORT.md`)、边界条件研究 (`HERMES_LOOP_CONDITIONS.md`)。 |
| **阶段 2：设计 (Design)** | 系统架构与模块设计 | [`07-Architecture/`](../07-Architecture/) | 高级系统架构、五层循环设计 (`ITERATION_LOOP_DESIGN.md`)、多轮交互工作流 (`CONVERSATION_LOOP_WORKFLOW.md`)、颠覆性特性架构 (`MAJOR_GAME_CHANGER_FEATURES_ARCH.md`)。 |
| **阶段 3：规范 (Specification)** | BDD 测试规范与验收标准 | [`08-Specification-Driven-Development/`](../08-Specification-Driven-Development/) | 可测试的行为驱动规范 (`GAME_CHANGER_FEATURES_SPECS.md`, `AGENT_LOOP_BDD_SPECS.md`)，充当设计与代码之间的契约。 |
| **阶段 4：规划 (Planning)** | 任务清单与 Sprint 追踪 | [`14-Tasks/`](../14-Tasks/) | Master 任务 Backlog、Sprint 迭代项、完成进度追踪 (`GAME_CHANGER_FEATURES_TASKS.md`)。 |
| **阶段 5：实施 (Implement)** | 编码实现与服务构建 | [`implementations/`](../implementations/) | C# 核心库与 WebAPI/gRPC 服务封装 (`implementations/csharp/`)、F# 核心库 (`implementations/fsharp/`)。 |
| **阶段 6：验证 (Verify)** | 自动化测试与质量门禁 | [`09-Testing/`](../09-Testing/) | 自动化测试套件 (Expecto 60/60, xUnit 20/20)、代码质量止步门禁 (`pre_verify`)、持续集成脚本。 |
| **阶段 7：知识沉淀 (Knowledge)** | 业务知识库与沉淀 | [`03-Knowledge-System/`](../03-Knowledge-System/) | 领域术语词典、平台 Sitemap、实施后运维与业务知识沉淀 (`KNOWLEDGE_INDEX.md`)。 |

---

## 🛠️ 辅助工程资源目录

- **`00-Getting-Started/`**：开发者环境配置与快速入门。
- **`01-Engineering-Workflow/`**：工程工作流方法论与阶段规范。
- **`04-Skill-Library/` & `05-Prompt-Library/`**：可复用 Agent 技能与 System Prompt 库。
- **`06-Standards/`**：编码规范、F# 函数式迁移指南与 Git Commit 规范。
