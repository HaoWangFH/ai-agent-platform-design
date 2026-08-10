# Architecture & Design: 4 Paradigm-Shifting Game-Changer Agent Features

> **Document Version:** 2.1.0  
> **Target Platform:** Skight AI Agent Platform (C# & F#)  
> **Source Benchmarks:** Hermes Agent & Anthropic Claude Code  
> **Last Updated:** 2026-08-09

---

## 🧭 Executive Summary

Based on deep source-level analysis of **Hermes Agent** (`conversation_loop.py`, `delegation_context.py`, `memory_manager.py`) and **Claude Code** CLI, we specify 4 game-changing features that transform the platform from a simple tool-calling loop into a world-class, multi-agent autonomous engineering engine:

1. **Sub-Agent Delegation (`delegate_task`)**: Concurrent child agent fan-out with isolated context stacks.
2. **Pre-Verify Code Quality Stop Gate (`pre_verify`)**: Post-edit quality enforcement before yielding completed turns.
3. **Pre-API Steering Drain (`/steer`)**: Realtime mid-turn user direction injection without role alternation violation.
4. **Server-Ready Plugable Vector Memory (`IMemoryStore`)**: Dual-adapter memory architecture supporting local SQLite FTS5/vector and cloud PostgreSQL `pgvector` multi-tenant deployments.

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

---

### 2. Pre-Verify Code Quality Stop Gate (`pre_verify`)

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

### 4. Server-Ready Plugable Vector Memory (`IMemoryStore`)

```
                                  [ Agent Core (F# / C#) ]
                                              │
                                              ▼
                                   [ IMemoryStore Interface ]
                                              │
                    ┌─────────────────────────┴─────────────────────────┐
                    ▼                                                   ▼
       【Local CLI / IDE Mode】                                【Remote Web Server Mode】
        SqliteMemoryStore                                       PgVectorMemoryStore
  (Zero-dep, single ~/.skight/memory.db)                  (PostgreSQL + pgvector + tsvector)
```

#### F# Memory Signature (`MemoryStore.fs`)
```fsharp
type MemoryQuery = {
    UserId: string
    SearchText: string
    Vector: float32[] option
    Limit: int
}

type MemoryRecord = {
    Key: string
    Value: string
    Score: float32
}

type IMemoryStore =
    abstract member StoreAsync: userId: string -> key: string -> value: string -> Async<unit>
    abstract member SearchAsync: query: MemoryQuery -> Async<MemoryRecord list>
```

#### C# Memory Signature (`IMemoryStore.cs`)
```csharp
namespace Skight.AgentPlatform
{
    public record MemoryQuery(string UserId, string SearchText, float[]? Vector = null, int Limit = 5);
    public record MemoryRecord(string Key, string Value, float Score);

    public interface IMemoryStore
    {
        Task StoreAsync(string userId, string key, string value);
        Task<IReadOnlyList<MemoryRecord>> SearchAsync(MemoryQuery query);
    }
}
```
