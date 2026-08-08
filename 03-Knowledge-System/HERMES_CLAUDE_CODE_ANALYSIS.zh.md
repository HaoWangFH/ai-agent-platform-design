# 架构分析：Hermes Agent 与 Claude Code 高级特性深度对比

> **文档版本：** 1.0.0  
> **目标平台：** Skight AI Agent Platform (C# & F#)  
> **更新时间：** 2026-08-08

---

## 🧭 执行摘要

在完成了纯函数 `RunAsync` 循环、MCP 协议客户端、工具安全沙箱、人工确认门禁（Approval Guard）以及媒体与自动化工具链的基础实现之后，本文档深度对标 **Hermes Agent** 开源架构与 Anthropic 官方 **Claude Code** CLI 工具，提炼出可用于下一步增强的 7 项核心高级特性模式。

通过对比分析两者的运行机制，我们梳理出了传统 Agent 平台普遍缺失的高高级可靠性机制。这些特性将显著提升 Agent 在面对 API 截断、异常错误时的自愈能力、测试验证质量以及多 Agent 协作扩展能力。

---

## 🔬 机制对比矩阵

| 特性模式 | Hermes Agent 机制 | Claude Code 机制 | 拟议平台架构设计 | 影响 | 优先级 |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **1. 长度截断自动续写 (Length Truncation)** | 检查 `finish_reason == "length"`，追加 `"Please continue..."` 并自动循环（上限 4 次） | 自动流式拼接续写请求，保留未完成的工具调用 JSON 片段 | `LengthContinuationHandler`：拦截 `length` 退出码，缝合片段并追加续写提示词 | 🔴 高 | **P0** |
| **2. 修改后验证门禁 (`pre_verify`)** | 检测文件修改标记 (`file_edited=True`)，拦截完成响应并注入验证提示 | 在产出最终回答前自动执行 Lint 与单元测试 | `VerificationGate`：追踪本轮文件修改状态；若未验证则提示 Agent 必须先运行测试 | 🔴 高 | **P0** |
| **3. 消息序列与角色修复** | 在 API 序列化前，自动为孤立的 `tool_call_id` 补全合成工具响应 | 严格的消息栈清洗器，确保 `user/assistant/tool` 交替顺序 | `MessageSequenceSanitizer`：请求前置转换器，修复孤立工具调用与非法连续角色 | 🟡 中 | **P1** |
| **4. 多 Provider 故障转移** | `_try_activate_fallback()` 在 429/500/内容策略错误时自动切换 Provider | 在主备 Endpoint 间透明无缝故障转移 | `FailoverLlmCaller`：`LlmCaller` 装饰器，在配额或暂态错误时按备选链重试 | 🟡 中 | **P1** |
| **5. 动态工具掩码与作用域** | 任务完成后静音维护工具；限制递归 `delegate_task` 深度 | 按执行阶段限制工具（例如研究阶段仅开放只读工具，写代码时开放编辑工具） | `DynamicToolFilter`：感知阶段的工具注册表过滤器，按轮次暴露允许的 Schema | 🟡 中 | **P2** |
| **6. Token 级摘要压缩** | `should_compress(tokens)` -> 摘要 LLM 压缩历史对话 | `/compact` 命令与上下文压力阈值自动摘要 | `SummaryContextCompressor`：用 LLM 生成的对话摘要替代简单的条数截断 | 🟢 低 | **P2** |
| **7. 子 Agent 隔离与任务授权** | 派生带有独立预算的子 Agent 循环 | 派生独立的 CLI 子会话进行大范围搜索/后台命令 | `SubAgentDelegator`：用独立 session 和干净上下文调用 `runTurnAsync` 子会话 | 🟢 低 | **P3** |

---

## 📐 高优先级特性详细设计与签名

### 特性 1：长度截断自动续写 (P0)

#### 痛点
当 LLM 输出达到 `max_tokens` 限制时，API 会返回 `finish_reason = "length"`。传统 Agent 会直接崩溃或向用户返回不完整的回答/破损的 JSON 参数。

#### 架构设计
```fsharp
type LengthContinuationState = {
    ContinuationAttempts: int
    MaxAttempts: int
}

let handleLengthTruncation (response: LlmTurnResponse) (state: TurnState) : StepResult<TurnState, TurnResult> =
    if response.FinishReason = "length" then
        if state.LengthRetries < 4 then
            let nudgeMsg = UserMessage "Your previous response was cut off due to max_tokens limit. Please continue exactly from where you left off."
            Continue { state with Messages = state.Messages @ [ AssistantMessage(response.Content, response.ToolCalls); nudgeMsg ]
                                 LengthRetries = state.LengthRetries + 1 }
        else
            Exit { Outcome = TurnOutcome.Failed (FailureReason.NoResponse "Max length continuation retries exhausted"); Messages = state.Messages; ApiCalls = state.ApiCalls }
    else
        Continue state
```

---

### 特性 2：修改后验证门禁 (`pre_verify`) (P0)

#### 痛点
AI Agent 经常在修改完代码或文件后，未经编译或测试验证就直接结束本轮对话（Yield Completed），把带有语法错误的代码交付给用户。

#### 架构设计
```fsharp
type VerificationState = {
    FilesModifiedInTurn: bool
    VerificationPerformed: bool
}

let checkVerificationGate (filesModified: bool) (verificationPerformed: bool) (responseContent: string) (state: TurnState) : StepResult<TurnState, TurnResult> =
    if filesModified && not verificationPerformed then
        printfn "  [Verification Gate] Agent modified files but did not run verification tests. Injecting nudge..."
        let verifyNudge = UserMessage "You modified files during this turn. Please run tests or build verification commands to ensure your changes work cleanly before completing."
        Continue { state with Messages = state.Messages @ [ AssistantMessage(responseContent, []); verifyNudge ] }
    else
        Exit { Outcome = TurnOutcome.Completed responseContent; Messages = state.Messages; ApiCalls = state.ApiCalls }
```

---

### 特性 3：消息序列与角色修复 (P1)

#### 痛点
如果 `tool` 类型的消息没有紧跟在带有匹配 `tool_calls` 的 `assistant` 消息之后，OpenAI / Azure API 会直接抛出 `400 Bad Request` 导致会话中断。

#### 架构设计
```csharp
public static List<AgentMessage> SanitizeMessageSequence(List<AgentMessage> messages)
{
    var sanitized = new List<AgentMessage>();
    var pendingToolIds = new HashSet<string>();

    foreach (var msg in messages)
    {
        if (msg is AssistantMessage assistant)
        {
            foreach (var tc in assistant.ToolCalls) pendingToolIds.Add(tc.Id);
            sanitized.Add(msg);
        }
        else if (msg is ToolMessage tool)
        {
            if (pendingToolIds.Contains(tool.ToolCallId))
            {
                pendingToolIds.Remove(tool.ToolCallId);
                sanitized.Add(msg);
            }
            // 丢弃没有对应 assistant tool_calls 的孤立工具消息
        }
        else
        {
            // 在遇到 system/user 消息之前，为尚未调用的 tool_call 注入合成工具响应
            foreach (var missingId in pendingToolIds)
            {
                sanitized.Add(new ToolMessage(missingId, "Error: Tool execution cancelled or missing result."));
            }
            pendingToolIds.Clear();
            sanitized.Add(msg);
        }
    }
    return sanitized;
}
```

---

## 🎯 验证标准与成功指标

1. **长度续写**：当 LLM 输出触顶时，不引发 JSON 解析崩溃，自动续写率 100%。
2. **验证门禁**：只要本轮修改了文件，在发出 `Completed` 之前强制促使 Agent 执行构建/测试工具。
3. **消息清洗器**：在所有多轮会话中实现 0% 的 `400 Invalid Message Sequence` 格式错误。
4. **多 Provider 故障转移**：当主 Endpoint 返回 `429 Rate Limit` 时，无感自动切到备用模型。
