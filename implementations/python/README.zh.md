# Python Agent 实现指南

> **映射至抽象工作流规范：** [CONVERSATION_LOOP_WORKFLOW.zh.md](../../docs/CONVERSATION_LOOP_WORKFLOW.zh.md)

## 概述

Python 实现遵循 4 阶段 Agent 会话循环工作流，采用了 Python 数据类（dataclasses）、类型提示（type hints）以及标准 OpenAI SDK。

## 文件结构

- `agent.py`: 实现 4 阶段循环的 `Agent` 类与 `TurnResult` 数据类。
- `registry.py`: 用于工具 Schema 提取和工具执行的 `ToolRegistry`。
- `tools.py`: 通过装饰器/方法调用注册的工具定义。
- `main.py`: 交互式 CLI 循环入口点。

## 工作流映射

### 1. 阶段 1：回合序言 (Turn Prologue)
- 在 `Agent.run(user_input: str) -> TurnResult` 中初始化。
- 将用户输入追加到 `self.messages`。
- 重置每回合计数器：`api_call_count = 0`、`self._interrupt_requested = False`、`empty_content_retries = 0`。

### 2. 阶段 2：主会话循环 (Main Conversation Loop)
- **2.1 API 前检查：** 在 `while api_call_count < self.max_iterations:` 开头检查 `_interrupt_requested` 和预算限制。
- **2.2 消息准备：** `_prepare_api_messages()` 浅拷贝 `self.messages` 生成 `prepared_messages`。
- **2.3 上下文窗口保护：** 当 `len(messages) > context_window_limit` 时，`_compress_context_if_needed()` 裁剪中间历史。
- **2.4 内部重试循环：** 带 `time.sleep(2 ** retry)` 的 `for retry in range(self.max_retries)` 循环。
- **2.5 响应规范化：** 访问 `response.choices[0].message`。
- **2.6 工具执行路径：** 
  - 验证 `name in registry._tools`（未注册工具自我纠正）。
  - 通过 `json.loads` 验证 JSON 解析。
  - 带有 `try...except` 异常处理的工具执行。
  - 追加工具结果（`role="tool"`）并继续循环。
- **2.7 最终文本响应路径：**
  - 带提示词推动的空响应恢复。
  - 返回 `TurnResult(completed=True, exit_reason="text_response")`。

### 3. 阶段 3 & 4：回合终结 (Turn Finalization)
- 返回结构化的 `TurnResult` 对象。
