# GitHub Copilot 自定义指令

你正作为 AI-EOS (AI 工程操作系统) 的阶段 5 (实施阶段) 运行。
当在本代码库 (`projects/ai-agent-platform-design`) 中生成、重构或评审代码时，你必须严格遵守以下规则：

## 通用指南
- **规范优先 (阶段 3)**：在编写任何核心逻辑前，首先查看 `05-Specification-Driven-Development/` 中的相关 BDD 测试规范（例如 `GAME_CHANGER_FEATURES_SPECS.md`）。不得幻觉出与规范相违背的行为。
- **遵循架构 (阶段 2)**：理解本项目采用 5 层韧性架构循环。不得将循环简化为基础 `while` 循环。若修改循环逻辑，请参考 `04-Architecture/ITERATION_LOOP_DESIGN.md`。
- **语言上下文 (阶段 5)**：本库包含 C# 与 F# 双语言实现 (`implementations/csharp` 与 `implementations/fsharp`)。仅在 F# 目录中使用 F# 函数式范式（不可变状态、联合类型、模式匹配），在 C# 目录中使用面向对象范式。
- **测试与质量门禁 (阶段 6)**：当被要求编写测试时，始终使用 `07-Testing/TEST_SKELETON_GUIDE.md` 中提供的测试骨架（C# 使用 xUnit，F# 使用 Expecto），并支持 `pre_verify` 代码质量门禁。
- **知识沉淀 (阶段 7)**：将新探索出的领域概念或 Bug 解决方案记录到 `08-Knowledge-System/KNOWLEDGE_INDEX.zh.md`。
- **双语文档规范**：生成或更新文档时，必须始终同时生成英文版 (标准的 `.md`) 与中文版 (包含 `.zh.md` 后缀)。

## 解决方案指南
- `AI-EOS.Docs` 项目为纯文档节点，不应作为常规 Solution 构建的一部分进行编译。
