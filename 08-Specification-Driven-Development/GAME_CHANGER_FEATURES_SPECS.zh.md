# BDD 验收规范说明书：四大颠覆性 Agent 战略特性

> **目标实现：** `Skight.AgentPlatform` (C#) 与 `Skight.AgentPlatform.FSharp` (F#)  
> **更新时间：** 2026-08-09

---

## 🎯 特性 1：子 Agent 任务授权 (`delegate_task`)

```gherkin
功能: 子 Agent 任务授权
  作为 AI Agent 平台架构师
  我希望主 Agent 能够将子任务授权给具有独立上下文栈的子 Agent
  使得复杂的调研与多步骤任务不会污染主对话历史记录。

  场景: 单个子 Agent 任务授权
    假设 存在一个主 Agent Turn 会话
    当 LLM 调用工具 delegate_task，参数包含 goal "分析用于 Bug 修复的 git diff"
    那么 应该用独立的 AgentSessionState 初始化一个子 Agent 循环
    并且 子 Agent 应该在不超过其迭代预算的前提下执行
    并且 子 Agent 的最终文本结果应该作为一个 ToolMessage 返回给主 Agent。

  场景: 批量并行子 Agent 任务授权
    假设 存在一个主 Agent Turn 会话
    当 LLM 调用工具 delegate_task，参数包含包含 2 个任务目标 (goal) 的批量列表
    那么 2 个子 Agent 循环应该并发执行
    并且 它们的汇总总结应该作为一个统一的 ToolMessage 返回给主 Agent。
```

---

## 🎯 特性 2：代码质量止步门禁 (`pre_verify`)

```gherkin
功能: 代码质量止步门禁
  作为 AI Agent 平台
  我希望防止 Agent 在文件修改未验证的情况下结束 Turn
  以确保代码变更在完成之前保证通过测试。

  场景: 拦截在修改文件后未执行测试的完成 Turn
    假设 存在一个修改了文件的活跃 Agent Turn 会话
    并且 在修改后未执行任何验证测试工具
    当 Agent 尝试输出最终文本完成回答
    那么 Pipeline 应该拦截该完成回答
    并且 Pipeline 应该注入 User 提示词 "You modified files during this turn. Please run tests or build verification commands."
    并且 Pipeline 应该执行下一次迭代。
```
