# 03-Knowledge-System：知识库与领域指南

> **定位与职责：** 存放平台演进过程中积累的业务知识、AI Agent 设计模式、术语表及可复用知识资产。

---

## 📚 知识索引目录

1. **AI Agent 平台架构知识**：
   - 所有架构设计、对话循环规范、与标杆框架对齐分析报告，请参阅 [`07-Architecture/`](../07-Architecture/)。

2. **规范与 BDD 行为驱动设计**：
   - BDD 测试规范与特性规格说明，请参阅 [`08-Specification-Driven-Development/`](../08-Specification-Driven-Development/)。

3. **全景任务清单 Backlog**：
   - 当前与后续演进的任务清单，请参阅 [`14-Tasks/`](../14-Tasks/)。

4. **核心领域术语与概念**：
   - **代码质量止步门禁 (`pre_verify`)**：当代码/文件发生变更但未运行单元测试或构建验证时，自动拦截 Turn 结束状态并提示补全验证。
   - **API 前置转向注入 (`/steer`)**：在 API 调用前清空转向队列，搭载于 Tool 消息末尾以严格维持 OpenAI API 的 `user -> assistant -> tool -> user` 角色交替规范。
   - **上下文自动压缩引擎 (`context_compressor`)**：在 Token 消耗达 80% 窗口限制时，自动剪枝中间轮次并生成 `[TURN SUMMARY]` 总结。
   - **交互式对齐网关 (`clarify_tool`)**：结构化问答卡片机制，具备无人值守/非交互模式下的默认选项降级机制。
