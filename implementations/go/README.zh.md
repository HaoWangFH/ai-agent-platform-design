# Go Agent 实现指南

> **映射至抽象工作流规范：** [CONVERSATION_LOOP_WORKFLOW.zh.md](../../docs/CONVERSATION_LOOP_WORKFLOW.zh.md)

## 概述

Go 实现使用了 Go 语言惯用的结构体、`context.Context` 取消/超时控制以及 `github.com/sashabaranov/go-openai` 建模 4 阶段 Agent 会话循环工作流。

## 文件结构

- `agent/loop.go`: `Agent` 结构体、`TurnResult` 结构体以及 4 阶段循环执行。
- `agent/registry.go`: 管理 Go 函数处理器和参数 JSON 的 `ToolRegistry`。
- `main.go`: 交互式 CLI 入口点。

## 工作流映射

### 1. 阶段 1：回合序言 (Turn Prologue)
- 在 `(a *Agent) Run(ctx context.Context, userInput string) (*TurnResult, error)` 中执行。
- 将 `openai.ChatMessageRoleUser` 追加到 `a.messages`。
- 重置每回合状态：`apiCalls = 0`、`a.interruptRequested = false`、`emptyContentRetries = 0`。

### 2. 阶段 2：主会话循环 (Main Conversation Loop)
- **2.1 API 前检查：** 在 `for apiCalls < a.MaxIterations` 开头检查 `a.interruptRequested` 和 `MaxIterations`。
- **2.2 消息准备：** `prepareApiMessages()` 创建 `a.messages` 的浅拷贝切片。
- **2.3 上下文窗口保护：** 当 `len(msgs) > a.ContextWindowLimit` 时，`compressContextIfNeeded()` 裁剪中间消息。
- **2.4 内部重试循环：** API 出错时带指数级 `time.Sleep` 的重试循环。
- **2.5 响应规范化：** 访问 `resp.Choices[0].Message`。
- **2.6 工具执行路径：**
  - 检查工具是否已注册（未注册工具自我纠正）。
  - 使用 `json.Unmarshal` 验证 JSON 参数。
  - 执行工具处理器并将错误字符串捕获到 `openai.ChatMessageRoleTool` 内容中。
  - 继续循环发送工具结果回 LLM。
- **2.7 最终文本响应路径：**
  - 在返回后备文本 `"(empty response)"` 之前用提示词推动处理空文本响应。
  - 返回带有 `Completed: true, ExitReason: "text_response"` 的 `TurnResult`。

### 3. 阶段 3 & 4：回合终结 (Turn Finalization)
- 返回结构化的 `*TurnResult` 指针。
