# GitHub Copilot 自定义指令

你是 AI-EOS（AI 工程操作系统）中的 Phase 5 (实施阶段)。
当你在本仓库 (`projects/ai-agent-platform-design`) 中生成、重构或审查代码时，必须严格遵守以下规则：

1. **先阅读规范**：在实现任何核心逻辑之前，请在 `08-Specification-Driven-Development/` 目录下查找相关的 BDD 规范（例如 `AGENT_LOOP_BDD_SPECS.zh.md`）。请勿捏造与这些规范相矛盾的行为。
2. **遵循架构**：了解本项目使用的是 5 层弹性架构循环。请勿将循环简化为基本的 `while` 循环。如果你正在修改循环，请参考 `07-Architecture/ITERATION_LOOP_DESIGN.zh.md`。
3. **语言上下文**：本仓库包含 C# 和 F# 两种实现 (`implementations/csharp` 和 `implementations/fsharp`)。在 F# 文件夹中只能使用 F# 惯用法（不可变状态、可区分联合、模式匹配），在 C# 文件夹中只能使用 C# 的面向对象惯用法。
4. **测试 (Phase 6)**：当被要求编写测试时，始终使用 `09-Testing/TEST_SKELETON_GUIDE.md` 提供的骨架（C# 使用 xUnit，F# 使用 Expecto）。
5. **双语文档**：在生成或更新文档时，必须始终生成两个版本：一个英文版本（正常的 `.md` 命名）和一个中文版本（带有 `.zh.md` 后缀）。
