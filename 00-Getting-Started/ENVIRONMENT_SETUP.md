# Environment Setup

According to the AI-EOS Phase 0 specifications, environment setup should be a one-time process. The following is the basic environment required to participate in the `ai-agent-platform-design` (C# and F# implementations):

## 1. Core Development Tools (Human)
- **Visual Studio 2026** or **VS Code** (Recommended with C# Dev Kit and Ionide for F# extensions)
- **.NET 8.0 SDK** (or a higher version required by the project)

## 2. AI Assistance Tools
- **GitHub Copilot**: Used for Phase 5 (Implementation), providing real-time code completion and refactoring within the IDE.
- **Gemini / ChatGPT**: Used for Phases 1-4 and Phase 7, handling long-context synthesis, architecture design, and specification writing.

## 3. Cloud & Integration Environment (Azure)
- **Azure CLI**: Used for environment configuration and infrastructure deployment.
- **Docker**: If the Agent platform requires containerized deployment.
- **Bicep / Terraform**: Used for automated provisioning.

> **Note:** After completing this phase, you can focus on business logic and architecture design without needing to frequently modify the underlying environment.
