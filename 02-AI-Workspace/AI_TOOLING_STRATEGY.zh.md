# AI Tooling Strategy

在 AI-EOS 方法论中，我们不期望一个单一的模型或工具能够胜任所有工作。根据 `guidance.md` 的核心思想，我们将根据不同 AI 的优势在不同阶段分配不同的角色。

## 各阶段 AI 工具映射

| 阶段 (Phase) | 主要工具 (Primary Tool) | 为什么使用它 (Why) |
| --- | --- | --- |
| **0. Environment** | **Human** | 仅需一次性设置，自动化脚本辅助。 |
| **1. Knowledge** | **Gemini** | 长上下文合成能力强，适合阅读海量文档和现有代码。 |
| **2. Requirements** | **Gemini + ChatGPT** | 梳理和结构化业务需求，将领域知识转化为功能列表。 |
| **3. Architecture** | **Gemini + ChatGPT** | 探讨设计方案、生成 ADR、系统图和 API 契约。 |
| **4. Specification** | **ChatGPT** | 编写 BDD 验收标准和测试计划。 |
| **5. Implementation**| **GitHub Copilot** | 在编辑器 (IDE) 内直接进行代码生成和重构。 |
| **6. Verification** | **Copilot + ChatGPT** | 测试生成、代码审查以及架构对齐检查。 |
| **7. Knowledge Capture**| **ChatGPT** | 总结项目经验、更新知识库并生成可重用的模板和 Prompt。 |

## 在本项目的应用
- 当你需要阅读和对标现有的 Hermes 代码库时，请在本项目文档库中使用 **Gemini** 分析。
- 当你需要编写具体的 C# 和 F# 代码逻辑时，请直接在 Visual Studio 中使用 **Copilot**。
