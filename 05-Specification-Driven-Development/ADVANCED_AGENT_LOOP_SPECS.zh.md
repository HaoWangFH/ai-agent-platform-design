# 规范说明书：高级 Agent 循环与高可靠自愈机制 (BDD 验收规范)

> **规范标准：** BDD Gherkin & 可执行测试要求  
> **目标实现：** `Skight.AgentPlatform` (C#) 与 `Skight.AgentPlatform.FSharp` (F#)  
> **更新时间：** 2026-08-08

---

## 🎯 规范 1：长度截断自动续写 (`finish_reason = "length"`)

```gherkin
功能: 长度截断自动续写
  作为 AI Agent 平台
  我希望 Agent 循环在检测到 LLM 响应因 Token 限制被截断时
  能够无缝发起续写请求，避免丢掉上下文或崩溃。

  场景: LLM 响应截断且 finish_reason 为 length
    假设 存在一个活跃的 Agent Turn 会话
    当 LLM API 返回 finish_reason = "length" 且包含部分内容 "详细实现细节如下：function processItem() {"
    那么 Agent Pipeline 不应该异常报错终止
    并且 Agent Pipeline 应该追加该部分 Assistant 消息
    并且 Agent Pipeline 应该追加 User 续写提示词 "Your previous response was cut off due to max_tokens limit. Please continue..."
    并且 Agent Pipeline 应该发起下一次 LLM 调用以接收剩余输出。

  场景: 长度续写重试达到最大阈值
    假设 存在一个 length_continuation_retries = 3 的活跃会话
    当 LLM API 连续第 4 次返回 finish_reason = "length"
    那么 Agent Pipeline 应该停止继续续写
    并且 Turn 的结果应该为 Failed，失败原因为 "Max length continuation retries exhausted"。
```

---

## 🎯 规范 2：修改后验证门禁 (`pre_verify`)

```gherkin
功能: 修改后验证门禁
  作为 软件开发 AI Agent
  我希望 Agent Pipeline 在结束 Turn 之前先验证文件修改
  以确保代码变更在声明完成前成功通过编译与单元测试。

  场景: Agent 修改了文件但试图在未经验证的情况下完成对话
    假设 存在一个活跃的 Agent 会话
    并且 Agent 执行了修改文件 "src/Types.fs" 的工具
    并且 在文件修改后未执行任何测试或构建工具
    当 LLM API 返回最终文本响应 "我已经更新了类型定义。"
    那么 Pipeline 应该拦截该完成响应
    并且 Pipeline 应该注入 User 提示词 "You modified files during this turn. Please run tests or build verification commands to ensure your changes work cleanly."
    并且 Pipeline 应该执行下一次迭代以允许 Agent 运行验证工具。

  场景: Agent 修改了文件并在完成前运行了验证工具
    假设 存在一个活跃的 Agent 会话
    并且 Agent 执行了修改文件 "src/Types.fs" 的工具
    并且 Agent 随后执行了运行 "dotnet test" 的终端工具
    当 LLM API 返回最终文本响应 "我更新了类型并验证所有测试均通过。"
    那么 Pipeline 应该接受该响应
    并且 Turn 的结果应该为 Completed，包含最终文本响应。
```

---

## 🎯 规范 3：消息序列与角色交替修复

```gherkin
功能: 自动消息序列与角色交替清洗
  作为 AI Agent 平台
  我希望在将消息历史发送给 LLM API 之前先对其进行清洗
  以确保无效的消息角色顺序或孤立的工具调用不会引发 HTTP 400 Bad Request 崩溃。

  场景: Assistant 消息包含 tool_calls 但缺乏匹配的 tool 响应
    假设 消息历史包含：
      | Role      | Content / Details                    |
      | system    | "You are an agent."                  |
      | user      | "Check system status."               |
      | assistant | tool_calls: [id: "tc_1", name: "sys"]|
      | user      | "Cancel that."                       |
    当 消息序列清洗器处理该历史记录时
    那么 应该在 User 消息之前注入一个 tool_call_id 为 "tc_1" 且内容为 "Error: Tool execution cancelled" 的合成 ToolMessage
    并且 最终的消息序列对于 OpenAI API 请求体提交必须是合法的。
```

---

## 🎯 规范 4：多 Provider LLM 故障转移链

```gherkin
功能: 多 Provider LLM API 故障转移
  作为 AI Agent 平台
  我希望 LLM Caller 在遭遇暂态故障时能自动尝试备用 Provider
  以保证频率限制 (429) 或节点故障不会导致 Agent 工作流崩溃。

  场景: 主模型 Endpoint 返回 429 Rate Limit
    假设 FailoverLlmCaller 配置了主模型 "gpt-4o" 和备用模型 "gpt-4o-mini"
    当 主 Caller 返回 ApiCallFailed "429 Rate Limit Exceeded"
    那么 FailoverLlmCaller 应该自动调用备用 Caller "gpt-4o-mini"
    并且 如果备用 Caller 成功，响应应该无缝返回给 Pipeline。
```
