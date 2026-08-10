# Architecture & Design: 4 Paradigm-Shifting Game-Changer Agent Features

> **Document Version:** 2.0.0  
> **Target Platform:** Skight AI Agent Platform (C# & F#)  
> **Source Benchmarks:** Hermes Agent & Anthropic Claude Code  
> **Last Updated:** 2026-08-09

---

## 🧭 Executive Summary

Based on deep source-level analysis of **Hermes Agent** (`conversation_loop.py`, `delegation_context.py`, `memory_manager.py`) and **Claude Code** CLI, we specify 4 game-changing features that transform the platform from a simple tool-calling loop into a world-class, multi-agent autonomous engineering engine:

1. **Sub-Agent Delegation (`delegate_task`)**: Concurrent child agent fan-out with isolated context stacks.
2. **Pre-Verify Code Quality Stop Gate (`pre_verify`)**: Post-edit quality enforcement before yielding completed turns.
3. **Pre-API Steering Drain (`/steer`)**: Realtime mid-turn user direction injection without role alternation violation.
4. **Persistent Vector Memory & Ephemeral Injection (`memory_manager`)**: Dual-layer persistent memory with zero prompt pollution.

---

## 📐 Detailed Architecture Specifications

### 1. Sub-Agent Task Delegation (`delegate_task`)

#### Architecture Flow
```
[ Lead Architect Agent ]
          │
          ├─► ToolCall: delegate_task(tasks=[{goal: "Search DB"}, {goal: "Audit Security"}])
          │
          ├───► Child Agent 1 (Isolated State, Bounded Budget=5) ──► Summary String ┐
          │                                                                         ├─► Aggregated Tool Result
          └───► Child Agent 2 (Isolated State, Bounded Budget=5) ──► Summary String ┘
```

#### F# Signature (`DelegateTool.fs`)
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

#### C# Signature (`DelegateTool.cs`)
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

### 2. Pre-Verify Code Quality Stop Gate (`pre_verify`)

#### Architecture Flow
```
[ Agent Decision: Yield Completed ]
          │
          ▼
   Files Modified in Turn? ─── NO ───► Yield TurnOutcome.Completed(finalText)
          │
         YES
          │
   Verification Executed in Turn? ─── YES ───► Yield TurnOutcome.Completed(finalText)
          │
          NO
          │
          ▼
   Inject User Nudge: "You modified files during this turn. Please run tests or build verification commands."
          │
          ▼
   Execute Next Loop Iteration (Continue)
```

---

### 3. Pre-API Steering Drain (`/steer`)

#### Architecture Flow
```
[ Steer Queue ] ──► Drain pending steer text
                         │
                         ▼
   Scan messages backwards for last role = "tool"
                         │
                         ├─► Found: Append marker "[User Steering]: <text>" to tool output
                         └─► Not Found: Hold pending for next tool batch (preserves role alternation)
```

---

### 4. Ephemeral Persistent Memory (`memory_manager`)

#### Architecture Flow
```
[ Persistent Storage (SQLite / KeyValue) ]
                         │
                         ▼
   Query Relevant Memory Key/Values or Vector Embeddings
                         │
                         ▼
   Inject Ephemeral Context System Block into API Payload (NOT saved to session DB)
                         │
                         ▼
   LLM API Execution ──► Strip Ephemeral Context
```
