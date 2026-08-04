# Agent 迭代循环 — BDD 验收测试规范 (SDD)

> **相关设计文档：** [ITERATION_LOOP_DESIGN.zh.md](../07-Architecture/ITERATION_LOOP_DESIGN.zh.md)

本文档基于 "五层弹性架构" 的设计，提取出具体的 BDD (Behavior-Driven Development) 测试用例。这些用例应用于 C# 和 F# 实现的自动化验收测试中（Phase 6 Verification）。

## 第 1 层：核心 Agent 循环 (ReAct 模式)

**场景：LLM 成功调用工具并基于结果回复**
- **Given** 一个包含问题 "天气如何" 的用户输入
- **When** 核心循环开始执行
- **And** LLM 决定调用 `get_weather` 工具
- **Then** 引擎必须拦截工具调用，执行该工具，并将结果作为 `role: tool` 消息返回给 LLM
- **And** 循环继续直到 LLM 生成最终文本回复
- **And** `api_call_count` 不能超过 `max_iterations`

## 第 2 层：输出恢复

**场景：文本响应被截断 (finish_reason = length)**
- **Given** LLM 生成了超长文本
- **When** API 返回结果包含 `finish_reason: "length"`
- **Then** 引擎应该向上下文自动追加提示（如 "请继续"）
- **And** 重新触发 API 调用，最多重试 4 次

**场景：API 出现空响应**
- **Given** API 调用成功，但返回内容为空且没有工具调用
- **When** 引擎解析结果
- **Then** 必须静默重试该请求（不向对话中添加额外错误提示）
- **And** 最多静默重试 3 次，之后抛出异常或进入降级逻辑

## 第 3 层：自我纠正

**场景：LLM 幻觉了不存在的工具**
- **Given** LLM 输出请求调用 `get_weather_forecast` (不存在)
- **When** 引擎尝试分发工具调用
- **Then** 拦截调用并生成错误结果（列出所有有效工具）
- **And** 将错误反馈给 LLM 让其在下一轮自我纠正
- **And** 这种自我纠正重试最多发生 3 次

**场景：工具调用参数无效 (Invalid JSON)**
- **Given** LLM 生成了不合规的 JSON 参数
- **When** 引擎尝试反序列化参数
- **Then** 触发第一阶段的静默重试（最多 3 次）
- **And** 如果仍然失败，则向上下文注入错误结果要求 LLM 修正

## 第 4 层：提供商故障转移

**场景：遇到 Rate Limit (429) 或 Provider Error (5xx)**
- **Given** 当前默认提供商为 OpenAI
- **When** 请求返回 429 Too Many Requests
- **Then** 引擎触发指数退避重试 (Exponential Backoff)
- **And** 如果多次重试失败，无缝故障转移到备用提供商 (例如 Azure OpenAI)

## 第 5 层：质量门控

**场景：对话历史超过上下文窗口限制**
- **Given** 对话的上下文 Token 数接近模型极限
- **When** 准备发起下一次 API 调用
- **Then** 触发压缩策略，移除最旧的消息，但保留系统提示语和最新的一对问答
