# 架构设计说明书：四大颠覆性 AI Agent 战略特性

> **文档版本：** 2.0.0  
> **目标平台：** Skight AI Agent Platform (C# & F#)  
> **对比对标：** Hermes Agent (`conversation_loop.py`, `delegation_context.py`) 与 Claude Code  
> **更新时间：** 2026-08-09

---

## 🧭 执行摘要

基于对 **Hermes Agent** 源码与 **Claude Code** 运行机制的深度拆解，我们设计了 4 项颠覆性的战略级 Agent 特性。这些特性将使平台从一个简单的 Tool-Calling 循环跃升为具备多 Agent 团队协作、质量闭环自愈与长效记忆的顶级 AI 工程师平台：

1. **子 Agent 任务授权 (`delegate_task`)**：支持并发派生具备独立上下文栈的子 Agent 节点。
2. **代码质量止步门禁 (`pre_verify`)**：修改代码后在产出最终回答前强制拦截并要求跑测试验证。
3. **API 前置转向注入 (`/steer`)**：在不违反角色交替规则的前提下，实现在线即时转向。
4. **长效向量记忆与临时注入 (`memory_manager`)**：双层持久化记忆与无污染上下文注入。

---

## 📐 详细架构设计与代码签名

### 1. 子 Agent 任务授权 (`delegate_task`)

#### 架构数据流
```
[ 主架构师 Agent (Lead Architect) ]
          │
          ├─► 调用工具: delegate_task(tasks=[{goal: "搜索数据库"}, {goal: "审计安全"}])
          │
          ├───► 子 Agent 1 (独立 Session, 限制预算=5) ──► 总结文本 ┐
          │                                                       ├─► 聚合为 ToolMessage 返回
          └───► 子 Agent 2 (独立 Session, 限制预算=5) ──► 总结文本 ┘
```

#### F# 函数签名 (`DelegateTool.fs`)
```fsharp
type DelegatedRole = Orchestrator | Leaf

type DelegationTask = {
    Goal: string
    Role: DelegatedRole
    MaxIterations: int option
}

type SubAgentRunner = DelegationTask -> AgentSessionState -> Async<string * AgentSessionState>

let executeDelegatedTaskAsync (runner: SubAgentRunner) (task: DelegationTask) (parentState: AgentSessionState) : Async<string> =
    async {
        let childInitialState = { Messages = [ SystemMessage (sprintf "You are a specialized sub-agent. Goal: %s" task.Goal) ]; PendingCommand = RunTurn }
        let! summary, _ = runner task childInitialState
        return summary
    }
```

#### C# 类定义 (`DelegateTool.cs`)
```csharp
public class DelegationTask
{
    public string Goal { get; set; } = string.Empty;
    public string Role { get; set; } = "leaf";
    public int MaxIterations { get; set; } = 5;
}

public static class DelegateTool
{
    public static async Task<string> ExecuteDelegateTaskAsync(
        DelegationTask task,
        Func<string, AgentSessionState, Task<(string Summary, AgentSessionState Updated)>> childAgentRunner,
        AgentSessionState parentState)
    {
        var childInitial = new AgentSessionState
        {
            Messages = new List<ChatRequestMessage> { new ChatRequestSystemMessage($"You are a specialized sub-agent. Goal: {task.Goal}") }
        };
        var (summary, _) = await childAgentRunner(task.Goal, childInitial);
        return summary;
    }
}
```

---

### 2. 代码质量止步门禁 (`pre_verify`)

#### 架构数据流
```
[ Agent 准备产出最终回答 Completed ]
          │
          ▼
   本轮是否修改了文件? ─── 否 ───► 输出 TurnOutcome.Completed(finalText)
          │
         是
          │
   本轮是否执行过验证/测试? ─── 是 ───► 输出 TurnOutcome.Completed(finalText)
          │
          否
          │
          ▼
   注入 User 提示词: "You modified files during this turn. Please run tests or build verification commands."
          │
          ▼
   继续下一次 Loop 迭代 (Continue)
```

---

### 3. API 前置转向注入 (`/steer`)

#### 架构数据流
```
[ 转向队列 (Steer Queue) ] ──► 清空 pending 转向文本
                                    │
                                    ▼
   倒序扫描历史消息，查找最后一个 role = "tool" 的消息
                                    │
                                    ├─► 存在: 追加标记 "[User Steering]: <text>" 到该 Tool 输出末尾
                                    └─► 不存在: 保持挂起等待下一批 Tool 输出 (维护角色交替规则)
```

---

### 4. 临时持久化记忆 (`memory_manager`)

#### 架构数据流
```
[ 持久化存储 (SQLite / 键值库) ]
                         │
                         ▼
   查询相关的 Memory Key/Values 或向量 Embeddings
                         │
                         ▼
   将临时 System 上下文块注入到 API Payload 中 (不保存至会话数据库)
                         │
                         ▼
   LLM API 执行 ──► 卸载临时上下文块
```
