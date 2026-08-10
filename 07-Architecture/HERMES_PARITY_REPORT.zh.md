# Hermes Agent 功能对齐与项目进度报告 (Parity & Progress Report)

> **代码库：** [ai-agent-platform-design](file:///c:/Users/hwang5/wiki/projects/ai-agent-platform-design)  
> **参考模型：** Hermes Agent `run_conversation` 架构 ([conversation_loop.py](file:///c:/Users/hwang5/wiki/raw/projects/hermes-agent/agent/conversation_loop.py))  
> **最后更新日期：** 2026-08-02

---

## 📊 总体进度摘要

| 功能分类 | Hermes 总特性数 | 已实现特性 | 未实现特性 | 完成度 % |
|---|:---:|:---:|:---:|:---:|
| **核心会话循环 (Core Loop)** | 8 | **7** | 1 | **87%** |
| **工具执行与恢复 (Tool & Recovery)** | 5 | **4** | 1 | **80%** |
| **上下文与记忆管理 (Context & Memory)** | 6 | **2** | 4 | **33%** |
| **提供商与重试故障转移 (Failover)** | 5 | **1** | 4 | **20%** |
| **平台基础设施 (Platform Infrastructure)** | 6 | **0** | 6 | **0%** |
| **总计 (TOTAL)** | **30** | **14** | **16** | **47%** |

---

## 1. ✅ 已实现功能 (14 项特性)

这些功能代表了 **Agent 核心引擎**，并在所有 5 种参考语言（**Python**、**TypeScript**、**C#**、**Go** 和 **F#**）中完整实现：

### A. 核心会话循环状态机 (阶段 1–4)
1. **回合序言初始化 (Phase 1)**：摄取用户输入，重置每回合计数器（`api_call_count = 0`），并维护规范化消息历史。
2. **API 前检查 (Step 2.1)**：
   - **迭代预算保护**：硬性上限保护（`max_iterations = 10`），防止工具无限循环执行。
   - **用户中断信号保护**：`request_interrupt()` 处理，干净地退出循环并标记 `interrupted = true`。
3. **API 载荷隔离 (Step 2.2)**：消息浅拷贝（`api_messages`），确保临时提示词调整永远不会污染规范化的会话历史。
4. **上下文窗口保护与历史裁剪 (Step 2.3)**：当消息数量超过 `context_window_limit` 时，自动裁剪中间历史，同时保留系统提示词（索引 0）和最近的回合。
5. **内部 API 重试循环与退避 (Step 2.4)**：带有指数退避（`2^retry`）的瞬态网络/API 错误重试。
6. **响应规范化 (Step 2.5)**：跨 LLM 提供商的标准内容、工具调用和完成原因映射。
7. **最终文本路径与空响应恢复 (Step 2.7)**：在应用后备文本 `"(empty response)"` 之前，对空文本响应进行自动提示词推动重试。
8. **回合终结 (Phase 3 & 4)**：结构化 `TurnResult` 输出，返回 `final_response`、`messages`、`api_calls`、`completed`、`failed`、`interrupted`、`exit_reason` 和 `error`。

### B. 工具执行与错误恢复
9. **动态工具 Schema 注册**：提取工具定义的 JSON Schema。
10. **异步工具执行**：跨运行语言的异步工具调用。
11. **未注册工具自我纠正**：当 LLM 调用不存在的工具时，返回合成工具错误消息并列出已注册工具，以便模型自我纠正。
12. **JSON 参数解析错误恢复**：捕获 JSON 解析失败并将诊断错误输出返回给 LLM。
13. **运行时工具异常处理**：捕获工具执行崩溃并格式化错误字符串，不会破坏 Agent 循环。
14. **TDD 单元测试套件**：涵盖 F#、C#、Python、TypeScript 和 Go 的完整单元测试覆盖。

---

## 2. ⏳ 待办事项 / 未实现功能 (16 项特性)

这些功能存在于 Hermes Agent 中，但尚未移植到本参考平台：

### 分类 A：高级提示词与上下文架构
1. **3 层系统提示词组装 (`system_prompt.py`)**：
   - *第 1 层 (Stable)*：核心 Persona 和 KV 缓存屏障。
   - *第 2 层 (Context)*：日期/时间、环境以及激活的工具 Schema。
   - *第 3 层 (Volatile)*：特定于会话的指令和临时用户状态。
2. **Anthropic 提示词缓存 (`cache_control`)**：
   - 在系统提示词和最近消息边界注入 `cache_control` 断点，在 Claude 模型上降低高达 75% 的输入 token 成本。
3. **MoA (Mixture-of-Agents) 聚合器**：
   - 并行执行对次要“参考” LLM 的后台查询，并将聚合结果提供给主 LLM 聚合器。
4. **引导命令 (`/steer`)**：
   - 当用户在 LLM 生成过程中发送命令时，在活动工具结果中注入实时引导标记。

### 分类 B：弹性、认证与故障转移链
5. **多提供商故障转移链**：
   - 当遇到账单错误、内容策略拒绝 (400) 或持续的 429 速率限制时，自动切换 LLM 提供商（例如：主 OpenAI → 备用 Anthropic → Azure OpenAI）。
6. **凭据池轮转**：
   - 遇到配额限制时从凭据池中轮转 API Key。
7. **自动 OAuth Token 刷新**：
   - 自动刷新 Azure Entra ID、Vertex GCP、Codex、Copilot 和 Nous 的过期 Token。

### 分类 C：特殊恢复与验证钩子
8. **Codex 中间确认恢复**：
   - 检测模型何时给出简短确认（“好的，我来做”）而不是进行工具调用，并自动推动其执行。
9. **验证停止门控 (`verify_on_stop` 与 `pre_verify`)**：
   - 若回合期间修改了文件，在终结响应前要求验证（例如运行测试/构建）。
10. **长度截断续写 (`finish_reason="length"`)**：
    - 当 LLM 文本输出因 `max_output_tokens` 被截断时自动发送续写提示词。

### 分类 D：平台基础设施与持久化
11. **SQLite 会话持久化 (`hermes_state.py`)**：
    - 跟踪会话、消息历史、FTS5 全文搜索以及父子会话树分支的 SQLite 数据库。
12. **双文件记忆系统 (`MEMORY.md` 与 `USER.md`)**：
    - 持久化的 Agent 笔记和用户画像记忆管理。
13. **技能发现与自我学习 (`skills/`)**：
    - 扫描、加载并引导 Agent 学习可复用技能（`skill_manage`）。
14. **多 Agent 协调 (Kanban 任务看板)**：
    - 带有 CAS 锁的多 Agent 委托共享任务看板（`delegate_task`）。
15. **Cron 调度器**：
    - 在 Cron 计划上执行 Agent 提示词的后台定时任务。
16. **插件架构与网关适配器**：
    - 插件钩子系统（`pre_api_request`、`post_tool_execute`）以及网关 TUI/ACP 适配器。
