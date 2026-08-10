# Master Architecture & Design: Next-Gen AI Agent Platform Features

> **Document Version:** 3.0.0 (Comprehensive Audit Edition)  
> **Target Platform:** Skight AI Agent Platform (C# & F#)  
> **Source Benchmarks:** Hermes Agent & Anthropic Claude Code  
> **Last Updated:** 2026-08-09

---

## 🧭 Executive Summary

Based on deep source-level analysis of **Hermes Agent** (`conversation_loop.py`, `delegation_context.py`, `context_compressor.py`, `checkpoint_manager.py`, `clarify_tool.py`) and **Claude Code** CLI, we specify a complete 2-tier feature matrix that elevates the platform into a world-class autonomous software engineering engine.

---

## 📊 Comprehensive Feature Matrix

### 🌟 Tier 1: Major Core Game-Changers (Primary Focus)
1. **Sub-Agent Delegation (`delegate_task`)** `[STATUS: IMPLEMENTED]`
   - Concurrent child agent fan-out with isolated context stacks and leaf depth control.
2. **Server-Ready Vector Memory (`IMemoryStore`)** `[STATUS: IMPLEMENTED]`
   - Dual-adapter memory architecture supporting local SQLite FTS5/Vector and remote PostgreSQL `pgvector` multi-tenant deployments.
3. **Pre-Verify Code Quality Stop Gate (`pre_verify`)** `[STATUS: SPEC'D]`
   - Post-edit quality enforcement before yielding completed turns.
4. **Pre-API Steering Drain (`/steer`)** `[STATUS: SPEC'D]`
   - Realtime mid-turn user direction injection into last tool output without breaking role alternation.

### 🚀 Tier 2: Advanced Enterprise Ecosystem Capabilities (Extended Roadmap)
5. **Context Compaction Engine (`context_compressor`)**
   - Automatic turn-summary compression when conversation history approaches token budget limits (e.g. 128k/200k tokens), preserving critical findings while pruning raw tool outputs.
6. **Interactive Clarification Gateway (`clarify_tool`)**
   - Structured multiple-choice alignment tool prompting the user for decisions when encountering ambiguous requirements or high-risk actions.
7. **Background Cron & One-Shot Scheduler (`cronjob_tools`)**
   - Reactive background timer/cron execution system triggering agent wakeups without blocking active turns.
8. **Autonomous Skill Evolution (`skill_manager`)**
   - Autonomous creation, AST linting, and dynamic prompt registration of reusable `SKILL.md` skill bundles.
9. **Checkpoint & Rollback Manager (`checkpoint_manager`)**
   - Automated git & session state milestone snapshotting allowing seamless rollback if an agent trajectory fails.

---

## 📐 Detailed Architectural Specifications

### 1. Sub-Agent Task Delegation (`delegate_task`)
```
[ Lead Architect Agent ]
          │
          ├─► ToolCall: delegate_task(tasks=[{goal: "Search DB"}, {goal: "Audit Security"}])
          │
          ├───► Child Agent 1 (Isolated State, Bounded Budget=5) ──► Summary String ┐
          │                                                                         ├─► Aggregated Tool Result
          └───► Child Agent 2 (Isolated State, Bounded Budget=5) ──► Summary String ┘
```

### 2. Server-Ready Vector Memory (`IMemoryStore`)
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

### 3. Context Compaction Engine (`context_compressor`)
```
[ Messages Array ] ──► Token Count > 80% Window Limit?
                            │
                            ├─► YES: Prune intermediate tool outputs & synthesize TurnSummary
                            └─► NO: Keep messages intact
```

### 4. Interactive Clarification Gateway (`clarify_tool`)
```
[ Agent Task Execution ] ──► Ambiguity Detected?
                                   │
                                   ▼
                   Call `clarify_tool` modal UI
                                   │
                                   ▼
                   User Selection ──► Resume Pipeline Turn
```
