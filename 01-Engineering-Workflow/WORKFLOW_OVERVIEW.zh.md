# AI Agent 平台工程工作流概述

本文档概括了 AI Agent 平台 (C# & F#) 如何映射到 AI-EOS 的全生命周期工程方法论。

## 各阶段职责分布

1. **03-Knowledge-System (知识库与需求)**
   - 存放原始分析报告，例如 `HERMES_PARITY_REPORT.zh.md`。
   - 这是业务逻辑的起点，用来向 LLM 提供背景上下文，分析我们当前具备的功能和现有系统 (Hermes) 的差距。

2. **07-Architecture (架构与设计)**
   - 存放从需求转化来的高级系统设计，包括多轮交互流、核心循环条件等。
   - 包含了从原 `docs/` 迁移过来的所有架构文件：
     - `CONVERSATION_LOOP_WORKFLOW.zh.md` (会话生命周期)
     - `ITERATION_LOOP_DESIGN.zh.md` (核心的五层架构循环设计)
     - `MULTI_TURN_TOOL_WORKFLOW.zh.md` (多轮工具调用工作流)
     - `HERMES_LOOP_CONDITIONS.zh.md` (边界条件判断逻辑)

3. **08-Specification-Driven-Development (规范驱动开发)**
   - 存放可测试的规范 (BDD / 验收标准)。例如基于 `ITERATION_LOOP_DESIGN` 导出的 `AGENT_LOOP_BDD_SPECS.zh.md`。
   - 这一层是实施 (Implementation) 和测试 (Verification) 之间的桥梁，使得测试可以被自动化。

4. **13-Agent-Framework (智能体框架)**
   - 专门存放具体的 Agent 能力规范。随着项目的拓展，如果抽离出通用的 Agent 接口或基类，可以在此沉淀。
   
5. **implementations/ (代码实施)**
   - 包含 C# 和 F# 两种不同语言的实现，分别处于实施阶段 (Phase 5)，直接依据 07 和 08 阶段的文档进行构建。
   - 其中语言特性设计 (如 F# 的函数式迁移) 放在 `07-Architecture/fsharp/` 内，而纯代码留在各自的源代码库。
