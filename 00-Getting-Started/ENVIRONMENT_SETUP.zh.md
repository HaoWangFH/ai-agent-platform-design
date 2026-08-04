# Environment Setup

按照 AI-EOS Phase 0 的规范，开发环境的搭建应该是一次性的。以下是参与 `ai-agent-platform-design` (C# 和 F# 实现) 所需的基础环境：

## 1. 核心开发工具 (Human)
- **Visual Studio 2026** 或 **VS Code** (推荐安装 C# Dev Kit 和 Ionide for F# 插件)
- **.NET 10.0 SDK** (或项目要求的更高版本)

## 2. AI 辅助工具
- **GitHub Copilot**：用于 Phase 5 (Implementation)，在 IDE 内进行实时的代码补全和重构。
- **Gemini / ChatGPT**：用于 Phase 1-4 和 Phase 7，处理长文本上下文合成、架构设计和规范编写。

## 3. 云与集成环境 (Azure)
- **Azure CLI**：用于环境配置和基础设施部署。
- **Docker**：如果 Agent 平台需要容器化部署。
- **Bicep / Terraform**：用于自动化配置。

> **注意：** 在完成此阶段后，你就可以专注于业务逻辑与架构设计，而无需再频繁更改底层环境。
