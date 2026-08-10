# 架构设计说明书全景版：下一代 AI Agent 平台特性指南

> **文档版本：** 3.0.0 (深度审计版本)  
> **目标平台：** Skight AI Agent Platform (C# & F#)  
> **对比对标：** Hermes Agent (`conversation_loop.py`, `context_compressor.py`, `checkpoint_manager.py`) 与 Claude Code  
> **更新时间：** 2026-08-09

---

## 🧭 执行摘要

基于对 **Hermes Agent** 源码全量模块与 **Claude Code** 核心功能的二次深度审计，我们梳理出完整的双层（2-Tier）战略特性矩阵。这一矩阵将引导平台从基础的 Tool-Calling 循环演化为全功能的自主软件工程引擎。

---

## 📊 平台全景特性矩阵

### 🌟 Tier 1：四大核心颠覆性特性 (当前主攻)
1. **子 Agent 任务授权 (`delegate_task`)** `[状态：已实现并测试]`
   - 支持多子 Agent 并发派生、独立上下文栈与叶子节点层级控制。
2. **云原生可扩展向量记忆 (`IMemoryStore`)** `[状态：已实现并测试]`
   - 适配本地 SQLite FTS5/Vector 与云端 PostgreSQL `pgvector` 多租户部署的双适配器架构。
3. **代码质量止步门禁 (`pre_verify`)** `[状态：规范已制定]`
   - 修改代码后在产出最终回答前强制拦截，要求先跑测试验证。
4. **API 前置转向注入 (`/steer`)** `[状态：规范已制定]`
   - 在不违反角色交替规则的前提下，将转向指令缝合到最后一个 Tool 输出末尾。

### 🚀 Tier 2：高级企业级生态扩展特性 (演进路线图)
5. **上下文自动压缩引擎 (`context_compressor`)**
   - 当对话历史逼近 Token 预算上限（如 128k/200k）时，自动将早期轮次压缩为结构化 `TurnSummary`，保留关键结论并剪枝冗余 Tool 输出。
6. **交互式对齐网关 (`clarify_tool`)**
   - 当遇到歧义需求或高风险操作时，弹窗提供结构化多选题供用户决策对齐。
7. **后台 Cron 与定时调度器 (`cronjob_tools`)**
   - 支持单次定时与周期性 Cron 后台任务，被动触发 Agent 唤醒与通知。
8. **技能自主进化与管理器 (`skill_manager`)**
   - 自动生成、AST 语法校验并动态注册复用型的 `SKILL.md` 技能包。
9. **检查点与回滚管理器 (`checkpoint_manager`)**
   - 自动针对 Git 和会话状态保存里程碑快照，当执行轨迹偏离时支持一键无缝回滚。

---

## 📐 详细架构数据流

### 1. 子 Agent 任务授权 (`delegate_task`)
```
[ 主架构师 Agent (Lead Architect) ]
          │
          ├─► 调用工具: delegate_task(tasks=[{goal: "搜索数据库"}, {goal: "审计安全"}])
          │
          ├───► 子 Agent 1 (独立 Session, 限制预算=5) ──► 总结文本 ┐
          │                                                       ├─► 聚合为 ToolMessage 返回
          └───► 子 Agent 2 (独立 Session, 限制预算=5) ──► 总结文本 ┘
```

### 2. 云原生可扩展向量记忆 (`IMemoryStore`)
```
                                  [ Agent 核心引擎 (F# / C#) ]
                                              │
                                              ▼
                                   [ IMemoryStore 统一抽象 ]
                                              │
                    ┌─────────────────────────┴─────────────────────────┐
                    ▼                                                   ▼
       【本地 CLI / IDE 插件模式】                              【远程 Web 服务器/云端模式】
        SqliteMemoryStore                                       PgVectorMemoryStore
  (零依赖, 单文件 ~/.skight/memory.db)                    (PostgreSQL + pgvector + tsvector)
```

### 3. 上下文自动压缩引擎 (`context_compressor`)
```
[ 消息列表 Messages ] ──► Token 占用 > 80% 窗口限制?
                                │
                                ├─► 是: 剪枝中间工具输出，提炼生成 TurnSummary
                                └─► 否: 保持原始消息完整
```

### 4. 交互式对齐网关 (`clarify_tool`)
```
[ Agent 执行任务 ] ──► 检测到模糊需求?
                              │
                              ▼
                调用 `clarify_tool` 结构化问答 UI
                              │
                              ▼
                用户点击选项 ──► 恢复 Pipeline 轮次
```
