# AI Tooling Strategy

In the AI-EOS methodology, we do not expect a single model or tool to be competent for all tasks. According to the core ideas in `guidance.md`, we assign different roles based on the strengths of different AIs at various stages.

## AI Tool Mapping per Phase

| Phase | Primary Tool | Why |
| --- | --- | --- |
| **0. Environment** | **Human** | One-time setup only, assisted by automation scripts. |
| **1. Knowledge** | **Gemini** | Strong long-context synthesis capabilities, suitable for reading massive documents and existing code. |
| **2. Requirements** | **Gemini + ChatGPT** | Organize and structure business requirements, converting domain knowledge into feature lists. |
| **3. Architecture** | **Gemini + ChatGPT** | Discuss design alternatives, generate ADRs, system diagrams, and API contracts. |
| **4. Specification** | **ChatGPT** | Write BDD acceptance criteria and test plans. |
| **5. Implementation**| **GitHub Copilot** | Perform code generation and refactoring directly within the editor (IDE). |
| **6. Verification** | **Copilot + ChatGPT** | Test generation, code reviews, and architecture alignment checks. |
| **7. Knowledge Capture**| **ChatGPT** | Summarize project experience, update knowledge bases, and generate reusable templates/prompts. |

## Application in this Project
- When you need to read and benchmark against the existing Hermes codebase, use **Gemini** for analysis within this project's documentation repository.
- When you need to write specific C# and F# code logic, use **Copilot** directly in Visual Studio.
