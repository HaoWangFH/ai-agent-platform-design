# AI Agent 平台架构设计分析

> 基于 Hermes Agent 和 Claude Code 源码分析，结合多语言平台架构设计讨论。
> 日期：2026-07-11

---

## 目录

1. [Hermes Agent 架构分析](#1-hermes-agent-架构分析)
2. [Claude Code 架构分析](#2-claude-code-架构分析)
3. [Hermes vs Claude Code 对比](#3-hermes-vs-claude-code-对比)
4. [构建轻量级 AI Agent CLI 的技术选型](#4-构建轻量级-ai-agent-cli-的技术选型)
5. [Workflow、数据库与领域模型设计](#5-workflow数据库与领域模型设计)
6. [多用户平台的数据库演进](#6-多用户平台的数据库演进)
7. [语言选型分析](#7-语言选型分析)
8. [多语言混合架构](#8-多语言混合架构)

---

## 1. Hermes Agent 架构分析

### 1.1 整体架构风格

窄腰架构（Narrow Waist Architecture）+ 插件化边缘扩展。核心只有两个东西：AIAgent（对话循环）和 ToolRegistry（工具注册表）。所有新能力都在边缘扩展，不改核心。

### 1.2 架构分层（由内到外）

```
┌─────────────────────────────────────────────────────┐
│                   入口/网关层                          │
│  CLI (cli.py) │ TUI Gateway │ Messaging Gateway     │
│  ACP Adapter  │ Web Dashboard                        │
├─────────────────────────────────────────────────────┤
│                   Agent核心层                         │
│  AIAgent (run_agent.py) — 对话循环、工具调用           │
│  System Prompt Builder │ Context Compressor           │
│  MemoryManager │ Conversation Loop                   │
├─────────────────────────────────────────────────────┤
│                  工具/能力层                           │
│  ToolRegistry ← 自注册工具模块                        │
│  model_tools.py (编排层)                              │
│  tools/*.py (terminal, file, web, browser, etc.)     │
├─────────────────────────────────────────────────────┤
│                  插件层（边缘扩展）                     │
│  plugins/web/ │ plugins/image_gen/ │ plugins/memory/  │
│  plugins/platforms/ │ plugins/browser/                │
│  plugins/model-providers/ │ plugins/video_gen/        │
├─────────────────────────────────────────────────────┤
│                  基础设施层                            │
│  hermes_state.py (SQLite) │ hermes_constants.py       │
│  providers/ (Provider抽象) │ cron/ (调度器)            │
│  utils.py │ hermes_time.py                            │
└─────────────────────────────────────────────────────┘
```

### 1.3 核心领域实体

| 实体 | 位置 | 说明 |
|------|------|------|
| AIAgent | run_agent.py | 核心Agent类，管理对话循环、工具执行、流式响应。接收40+回调参数 |
| Session | hermes_state.py | SQLite持久化会话，含FTS5全文搜索。支持parent_session_id链 |
| Message | hermes_state.py | 会话内消息历史，存储在SQLite中 |
| ToolEntry | tools/registry.py | 工具注册元数据：name, toolset, schema, handler, check_fn, is_async |
| Memory | tools/memory_tool.py | 双文件持久记忆：MEMORY.md（agent笔记 2200字符限）+ USER.md（用户画像 1375字符限） |
| Skill | tools/skill_usage.py | 可学习的技能文件，YAML frontmatter + Markdown |
| CronJob | cron/jobs.py | 定时任务，jobs.json持久化，croniter解析 |
| ProviderProfile | providers/base.py | 声明式模型提供商配置（dataclass） |
| Kanban Board | tools/kanban_tools.py | 多Agent协调看板，SQLite存储，CAS锁机制 |
| Profile | hermes_constants.py | 多Profile隔离（每个profile有独立的skills/plugins/cron/memories目录） |

### 1.4 实体关系

```
AIAgent ──1:N──> Session（通过hermes_state SQLite管理）
Session ──1:N──> Message
Session ──parent_session_id──> Session（压缩/分支/委托链）
AIAgent ──uses──> ToolRegistry（工具分发）
AIAgent ──uses──> MemoryManager ──delegates──> MemoryProvider（可插拔后端）
AIAgent ──creates──> AIAgent（delegate_task产生子Agent）
CronJob ──triggers──> AIAgent（通过scheduler.tick()）
GatewayRunner ──manages──> PlatformAdapter[] ──per-session──> AIAgent
ProviderProfile ──configures──> AIAgent（API模式、认证、quirks）
Plugin ──registers──> WebSearchProvider | ImageGenProvider | BrowserProvider | PlatformEntry | ProviderProfile
```

### 1.5 五个关键设计模式

#### 1. Prompt Cache 至上（冻结快照）

系统提示在会话生命周期内字节不变。Memory/Skills 启动时快照注入，会话内写入立即持久化到磁盘，但不改系统提示。下次会话才生效。保护 LLM 的 prefix cache。

#### 2. 自注册发现

每个 tools/*.py 在模块导入时调用 registry.register() 注册自身。model_tools.py 用 AST 静态分析扫描哪些文件含 registry.register() 调用，只导入这些。避免硬编码分发表。

#### 3. check_fn 门控可见性

工具通过 check_fn 动态控制是否出现在 schema 中。例如 kanban 工具只在 HERMES_KANBAN_TASK 环境变量设置时可见。check_fn 结果缓存 30 秒。

#### 4. 声明式 Provider

ProviderProfile 用 dataclass 描述提供商（不是继承），Transport 层（chat_completions/anthropic/codex）处理实际通信。

#### 5. 多入口统一核心

CLI/TUI/Gateway/ACP 四个入口共享同一 AIAgent 核心。平台差异全在网关层消化。

### 1.6 Gateway 消息路由

22+ 平台适配器继承 BasePlatformAdapter（4个抽象方法：connect/disconnect/send/get_chat_info）。

消息流：
```
平台消息 → 适配器 → GatewayRunner → session key 路由 
→ AIAgent 实例（LRU 缓存 max 128, TTL 1h）
→ 流式输出 → GatewayStreamConsumer（编辑/草稿模式）
→ 平台适配器 send() → 用户收到回复
```

会话隔离用 Python contextvars.ContextVar（asyncio 并发安全）。PII 脱敏：WhatsApp/Signal/Telegram 的 user_id/chat_id 用 SHA-256 哈希。

### 1.7 Relay 系统（实验性）

"Gateway 的 Gateway"。一个 RelayAdapter 通过 WebSocket 连接远程 Connector，代理所有平台。握手时接收 CapabilityDescriptor，零平台特定代码。Phase 5 新增 wake primitive（scale-to-zero 基础）。

### 1.8 工具注册与分发

```
tools/*.py → registry.register() → ToolRegistry（全局单例）
                                          ↓
model_tools.py ← get_tool_definitions(enabled, disabled)
                ← handle_function_call(name, args)
                                          ↓
run_agent.py AIAgent._process_tool_calls()
```

80+ 工具模块，30+ 个 toolset，支持嵌套组合。

### 1.9 Kanban 跨 Profile 协作

所有 profile 共享同一看板 SQLite。CAS（Compare-And-Swap）锁实现任务认领。Worker 只能操作自己的任务（_enforce_worker_task_ownership 防提示注入）。

### 1.10 Cron 调度

Gateway 后台线程每 60 秒调用 tick()。每个 job 启动独立子进程运行 agent。禁用 cronjob/messaging/clarify 工具集（防自我调度和交互阻塞）。

### 1.11 记忆系统

双文件 + 冻结快照 + 漂移检测。§ 分隔条目。磁盘被外部修改时拒绝变更并创建 .bak 备份。加载时扫描 promptware 模式。

---

## 2. Claude Code 架构分析

### 2.1 基本信息

- TypeScript + Bun 运行时，编译为原生二进制
- 1,902 个源文件，512K 行代码
- GitHub: github.com/anthropics/claude-code（81K+ stars）
- 源码架构于 2026年3月因 npm source map 泄漏被完整曝光

### 2.2 技术栈

| 层次 | 技术 |
|------|------|
| 运行时 | Bun（比 Node.js 快 3-5x） |
| 语言 | TypeScript（严格类型） |
| 终端 UI | React + Ink（React 渲染到终端，346 组件，104 hooks） |
| 布局引擎 | Yoga（Facebook Flexbox） |
| 类型验证 | Zod（运行时校验） |
| 安全分析 | tree-sitter（Bash AST 解析） |
| CLI 解析 | Commander.js |

### 2.3 核心执行模型：双层循环

- **外层**：对话循环 — 用户发消息启动新一轮
- **内层**：Agent 循环 (queryLoop() 异步生成器) — 持续调 Claude API，只要响应含 tool_use 就继续执行

工具通过 StreamingToolExecutor 并行执行（最多10个并发）。循环有10种终止原因。

### 2.4 工具系统

25+ 内置工具，分类：

| 类别 | 工具 |
|------|------|
| 读取 | Glob, Grep, Read, LS |
| 写入 | Write, Edit, MultiEdit |
| 执行 | Bash, Task（子代理） |
| Web | WebSearch, WebFetch |
| 工作流 | TodoWrite, NotebookEdit |
| 控制流 | EnterPlanMode, ExitPlanMode, AskUserQuestion |

**关键调度策略**：读操作并行，写操作串行。简单但有效，避免文件竞态。

### 2.5 系统提示组装 — 两区域缓存

**静态区域**（全局缓存，跨用户共享）：
1. 基础指令 + 安全规则（2900-4000 tokens）
2. ~/.claude/CLAUDE.md（全局偏好）
3. 项目级 CLAUDE.md
4. 工具定义 JSON schemas

**动态区域**（按会话缓存）：
5. 工作目录 + Git 状态
6. MCP 服务器能力（每轮重新计算）
7. 记忆文件、语言设置

启动时系统提示 + 工具定义占 30-40K tokens。

### 2.6 CLAUDE.md 上下文加载

三层结构：
- `~/.claude/CLAUDE.md` — 全局偏好
- 项目根/`CLAUDE.md` — 项目手册
- 项目根/`.claude/rules/` — 项目标准（提交到仓库，团队共享）

每一行每次请求都加载，建议 200 行以内。

### 2.7 权限模型 — 三层纵深防御

1. **ML 分类器**：处理 80% 常见场景自动判断
2. **规则引擎**：用户自定义策略（allow/ask/deny）
3. **确认对话框**：兜底人工确认

deny 规则不可被覆盖。权限由客户端代码强制执行。

### 2.8 子代理隔离

Task 工具生成的子代理完全隔离：不能读父代理对话历史，不能修改父代理状态，只接收 prompt + 工具列表，只返回文本结果。

---

## 3. Hermes vs Claude Code 对比

| 维度 | Claude Code | Hermes Agent |
|------|------------|--------------|
| 语言/运行时 | TypeScript + Bun | Python |
| 代码量 | 512K 行，1900+ 文件 | 轻量级 |
| UI | React+Ink 富终端 TUI | 无富TUI，面向对话/API |
| 工具调度 | 读并行/写串行调度器 | LLM 自行决定顺序 |
| 权限 | ML分类器+规则+人工 三层 | 基于配置控制 |
| 上下文管理 | 两区域缓存（静态+动态） | 冻结快照模式 |
| 多代理 | 内置 Task/Agent/Team 工具 | delegate_task + kanban 跨 Profile |
| 平台支持 | 终端+IDE | 22+ 消息平台 |
| 扩展 | plugins + skills + feature gates | plugins + skills + cron + Profile |
| 会话 | 文件系统 JSONL | SQLite + FTS5 全文搜索 |
| CLI实现 | React+Ink（整个CLI是React应用） | 分离架构：Python JSON-RPC后端 + React+Ink前端(ui-tui/) |
| 设计哲学 | 重量级终端原生 IDE 替代品 | 轻量级可编程多平台代理框架 |

**Hermes 独有优势**：Profile 系统、Kanban 跨 Profile 协作、Cron 定时任务、22+ 消息平台网关、Relay 中继、Memory 双文件持久记忆

**Claude Code 独有优势**：ML 权限分类器、读写分离调度器、React+Ink 富终端 UI、IDE WebSocket bridge、tree-sitter Bash 安全分析

**核心架构差异**：Claude Code 将 agent 逻辑和 UI 紧耦合在一个 React 应用中；Hermes 将 agent 核心与 UI 解耦——agent 是 headless 的，UI 是可插拔的。

---

## 4. 构建轻量级 AI Agent CLI 的技术选型

### 4.1 最简方案

OpenAI/Anthropic SDK + while 循环。Python 或 TypeScript，200 行。工具作为普通函数。

### 4.2 核心 Agent 循环

```python
while True:
    response = call_llm(messages, tools)
    if no tool_calls: break
    for call in tool_calls:
        result = dispatch(call)
        messages.append(result)
```

### 4.3 推荐最小技术栈

**Python 路线**：
- httpx 或 openai SDK（API 调用）
- click 或 typer（CLI）
- rich（终端格式化）
- SQLite（会话持久化）
- 工具函数 + JSON schema

**TypeScript 路线**：
- openai/anthropic SDK
- Commander.js（CLI）
- chalk（颜色）
- Better-sqlite3（持久化）

### 4.4 从 Hermes 和 Claude Code 学到的关键教训

1. 不要将 agent 逻辑耦合到 UI（Hermes 做对了）
2. 工具自注册模块比大 switch 语句更可扩展
3. 系统提示缓存对成本很重要 — 尽可能冻结
4. 先同步工具执行，需要时再加并行读/串行写
5. SQLite 足以应对一切（sessions, memory, kanban）

---

## 5. Workflow、数据库与领域模型设计

### 5.1 Workflow 两种模式

#### LLM 驱动（Hermes/Claude Code 模式）
LLM 决定下一步。"workflow" 编码在系统提示、工具描述和 skills 中。
- 优点：灵活，处理意外情况，易于变更
- 缺点：不确定性，难审计，昂贵
- 适用：创作、探索性编码、开放式任务

#### 代码驱动 + LLM 步骤（业务领域通常需要）
显式定义 workflow。LLM 在特定步骤处理判断/生成。
```
receive_order → validate_address(LLM) → check_inventory(DB) → 
select_carrier(rules+LLM) → generate_docs(LLM) → book_shipment(API)
```
- 适用：合规要求、可审计性、SLA 的业务流程

#### Workflow 框架推荐
- **Temporal**：持久化长时间运行的业务 workflow。崩溃恢复，重试，版本控制。
- **简单状态机**：线性流程用 dict {state: handler} 即可。
- 超过 10+ 状态且有分支时才考虑框架。

### 5.2 数据库选择分层

#### Tier 1: SQLite + 原始 SQL（起步）
简单 CRUD，参数化查询。WAL 模式处理并发读取。JSON1 处理半结构化数据。FTS5 处理搜索。

#### Tier 2: SQLAlchemy Core（需要多数据库时）
连接客户的 TMS、WMS、ERP — 不同数据库不同 schema。用 Schema Reflection 检查现有表：
```python
metadata = MetaData()
metadata.reflect(bind=erp_engine)
orders = metadata.tables['sales_orders']
```

#### Tier 3: 完整 ORM（agent 拥有数据模型时）
仅当 agent 是主要记录系统时。

### 5.3 领域模型设计原则

**错误方式**：传统实体-关系建模（建 ERP 而非 agent）

**正确方式**：围绕工具输入/输出建模

```python
# 面向工具的建模
class QuoteRequest(BaseModel):
    """Agent 获取运费报价所需的输入"""
    origin_zip: str
    dest_zip: str
    weight_kg: float
    dims_cm: tuple[float, float, float]
    service_level: Literal["express", "standard", "economy"]
```

使用 Pydantic 模型：JSON schema 生成（直接作为 LLM 工具定义）、运行时验证、序列化、文档。

### 5.4 领域知识组织

```
domain/
  schemas.py      # Pydantic 工具 I/O 模型
  queries.py      # 数据库查询
  integrations.py # 外部 API 客户端
  rules.py        # 业务规则（LLM 必须遵守）

tools/
  quote_tool.py       # uses schemas + integrations
  booking_tool.py     # uses schemas + integrations + rules
  tracking_tool.py    # uses schemas + queries
```

---

## 6. 多用户平台的数据库演进

### Stage 1: SQLite-per-user（< 100 用户）

```
/data/users/
  user-001/
    sessions.db
    kanban.db
    memories/
    config.yaml
  user-002/
    ...
```

每个用户写自己的 SQLite，无并发冲突。

### Stage 2: Postgres（100-10K 用户）

```sql
CREATE TABLE users (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  email TEXT UNIQUE NOT NULL,
  plan TEXT DEFAULT 'free',
  settings JSONB DEFAULT '{}'
);

CREATE TABLE sessions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id UUID NOT NULL REFERENCES users(id),
  parent_id UUID REFERENCES sessions(id),
  title TEXT,
  status TEXT DEFAULT 'active',
  metadata JSONB
);

-- Row-level security: 用户只能看到自己的数据
ALTER TABLE sessions ENABLE ROW LEVEL SECURITY;
CREATE POLICY user_sessions ON sessions
  USING (user_id = current_setting('app.user_id')::UUID);

CREATE TABLE messages (
  id BIGINT GENERATED ALWAYS AS IDENTITY,
  session_id UUID NOT NULL REFERENCES sessions(id),
  user_id UUID NOT NULL REFERENCES users(id),
  role TEXT NOT NULL,
  content TEXT,
  tool_calls JSONB,
  token_count INTEGER
);
```

**为什么 Postgres**：
- JSONB — 半结构化数据 + SQL 查询
- Row-Level Security — 数据库级用户隔离
- 内置全文搜索（tsvector）
- LISTEN/NOTIFY — 内置 pub/sub

### Stage 3: 平台规模（10K+ 用户）

- 读副本 + PgBouncer 连接池
- 多租户 org 模型（org_id 加到每张表）
- 工具沙箱（Docker/gVisor）
- 使用量计费追踪

### Agent 运行时并发方案

| 规模 | 方案 |
|------|------|
| < 50 并发 | ThreadPoolExecutor 进程内 |
| 50-500 并发 | ARQ/Celery + Redis worker 队列 |
| 500+ 并发 | K8s Job / serverless |

### 流式响应推送

Redis pub/sub channels per session。Agent 发布 tokens，客户端订阅。SSE（Server-Sent Events）比 WebSocket 更简单（单向流）。

---

## 7. 语言选型分析

### 7.1 各语言评估

#### Python

| 优势 | 劣势 |
|------|------|
| 最丰富的 AI/LLM 生态 | GIL 限制真正并发 |
| 最快原型速度 | asyncio 是后加的 |
| Pydantic 领域建模 | 类型系统可选且有漏洞 |
| FastAPI 生产就绪 | 内存占用高（~50-100MB/进程） |

**适用**：MVP 和小规模。平台规模需要多 worker 进程补偿。

#### TypeScript (Node/Bun)

| 优势 | 劣势 |
|------|------|
| 前后端统一语言 | AI/LLM 库生态第二 |
| 原生 async（事件循环默认） | 无真正多线程 |
| Bun 很快（Claude Code 证明） | 领域建模弱于 C#/F# |
| npm 生态庞大 | 运行时类型安全需要 Zod 额外层 |

**适用**：JS 原生团队。Claude Code 证明可行。

#### C#

| 优势 | 劣势 |
|------|------|
| **最佳 async 故事**（async/await 发源地） | AI/LLM SDK 生态第三 |
| 强类型系统 + 真泛型 | 比 Python 更多样板代码 |
| Records + pattern matching（C# 12+） | 社区偏企业向 |
| ASP.NET Core 最快主流 Web 框架 | |
| EF Core 成熟 ORM + 迁移 | |
| SignalR 实时流式推送 | |
| Channels 并发原语 | |
| 单二进制部署 | |
| Azure 原生生态 | |

```csharp
public record Session(
    Guid Id, Guid UserId, string Title,
    SessionStatus Status, DateTime CreatedAt
);

// Channels 并发
var channel = Channel.CreateBounded<AgentTask>(100);
await foreach (var task in channel.Reader.ReadAllAsync())
    await RunAgent(task);
```

**适用**：多用户平台、强类型业务领域、Azure 部署。

#### F#

| 优势 | 劣势 |
|------|------|
| **最佳领域建模**（区分联合、模式匹配） | 小社区（SO 答案仅 C# 的 1/50） |
| 管道操作符使工具链可读 | AI/LLM SDK 是 C# 的，互操作有时别扭 |
| 默认不可变 — 更少并发 bug | 招人困难 |
| 与 C# 同运行时、同性能、同部署 | IDE 支持弱于 C# |
| 可直接调用任何 C# 库 | |

```fsharp
type TaskStatus =
    | Pending
    | Running of claimedBy: string * expiresAt: DateTime
    | Blocked of reason: string
    | Done of summary: string * completedAt: DateTime
    | Failed of error: string * retryCount: int

// Agent 循环 — 干净、类型安全
let rec agentLoop ctx = async {
    let! response = callLLM ctx.Messages ctx.EnabledTools
    match response with
    | TextOnly text -> return appendMessage ctx text
    | WithToolCalls calls ->
        let reads, writes = calls |> List.partition isReadOnly
        let! readResults = reads |> List.map executeTool |> Async.Parallel
        let! writeResults = writes |> List.map executeTool |> Async.Sequential
        return! agentLoop (appendResults ctx readResults writeResults)
}
```

**适用**：独立开发者追求最优领域建模。生态风险是真实的。

#### Go

| 优势 | 劣势 |
|------|------|
| 最佳并发（goroutines + channels） | 领域建模极差（无 sum types、无 pattern matching） |
| 单静态二进制 | AI/LLM 生态薄 |
| 极低内存（~2KB/goroutine） | 不够表达复杂业务逻辑 |

**适用**：基础设施层（沙箱、容器管理、API 网关），不适合 agent 业务逻辑。

#### Rust

| 优势 | 劣势 |
|------|------|
| 最佳性能和内存安全 | 开发速度慢 3-5x |
| 好的类型系统 | AI 生态极少 |
| 无 GC 暂停 | 借用检查器与 agent 状态机冲突 |

**适用**：沙箱/工具执行层。不适合 agent 平台整体。

### 7.2 推荐路径

**Path A — C#（务实选择）**：ASP.NET Core + SignalR + EF Core + Postgres + Azure

**Path B — F# 核心 + C# 边缘（最优设计）**：F# 写 agent 循环/领域模型，C# 写 Web 框架/数据访问

**Path C — Python（如果要留在 Python）**：多 worker 进程补偿并发，Pydantic 严格使用，Litestar 替代 FastAPI

---

## 8. 多语言混合架构

### 8.1 每种语言用在最强的地方

```
┌─────────────────────────────────────────────────┐
│  Web UI / CLI                                    │
│  TypeScript (React/Next.js 或 Ink for CLI)       │
├─────────────────────────────────────────────────┤
│  API Gateway + 实时流式推送                       │
│  C# (ASP.NET Core + SignalR)                     │
├─────────────────────────────────────────────────┤
│  Agent Brain + 领域逻辑 + Workflow               │
│  F# (领域模型, 工具分发, 状态机)                  │
├─────────────────────────────────────────────────┤
│  AI/LLM 工具 + 领域库                            │
│  Python (LLM SDK, NLP, 数据处理)                 │
├─────────────────────────────────────────────────┤
│  工具沙箱 + 基础设施                              │
│  Go 或 Rust (隔离, 高并发基础设施)                │
└─────────────────────────────────────────────────┘
```

### 8.2 跨语言通信模式

#### Pattern 1: gRPC（最干净）

```protobuf
// agent.proto — 共享契约
service AgentService {
  rpc RunSession(SessionRequest) returns (stream AgentEvent);
  rpc ExecuteTool(ToolRequest) returns (ToolResponse);
}

message AgentEvent {
  oneof event {
    TokenDelta delta = 1;
    ToolCall tool_call = 2;
    ToolResult tool_result = 3;
    SessionComplete complete = 4;
  }
}
```

每种语言实现自己的部分：
- **C# API Gateway**：ASP.NET Core + SignalR，gRPC 调用 F# Agent
- **F# Agent Brain**：领域模型 + agent 循环 + 读写分离调度
- **Python Tools**：gRPC 服务暴露领域工具（LLM SDK, NLP, 数据处理）

#### Pattern 2: .NET 宿主 + Python 互操作

F# 和 C# 共享 .NET 运行时（零开销互操作）。Python 通过以下方式之一：
- **Python.NET**：进程内调用（无网络开销，但共享 GIL）
- **子进程 + stdin/stdout**：简单可靠，天然进程隔离（Hermes 调用 Claude Code 就是这种方式）
- **gRPC**：最正式的方式

#### Pattern 3: 消息队列（最可扩展）

```
C# API ──publish──► Redis/RabbitMQ ──consume──► F# Agent Workers
                                    ──consume──► Python Tool Workers
                                    ──consume──► Go Sandbox Workers
```

### 8.3 推荐实施阶段

#### Phase 1 — 两个服务

```
C#/F# 单体                    Python Sidecar
┌──────────────────┐          ┌──────────────┐
│ ASP.NET Core API │   gRPC   │ Python Tools │
│ SignalR streaming│◄────────►│ LLM SDKs     │
│ F# Agent Brain   │          │ NLP/Data     │
│ EF Core + Postgres│         │ Domain libs  │
│ Auth + Billing   │          └──────────────┘
└──────────────────┘
```

#### Phase 2 — 加 Go 沙箱

```
.NET 单体 ──gRPC──► Go Sandbox Service
                        │
                        ├── Docker container per user
                        ├── 资源限制 (CPU/mem/time)
                        └── 网络隔离
```

#### Phase 3 — 消息队列水平扩展

Redis/RabbitMQ 放在服务之间，各服务独立扩展。

### 8.4 项目结构

```
my-agent-platform/
│
├── proto/                    # 共享契约
│   ├── agent.proto
│   ├── tools.proto
│   └── sandbox.proto
│
├── src/
│   ├── Gateway/              # C# — ASP.NET Core API + SignalR
│   │   ├── Controllers/
│   │   ├── Hubs/
│   │   ├── Auth/
│   │   └── Program.cs
│   │
│   ├── Agent.Core/           # F# — 领域模型 + agent 循环
│   │   ├── Domain.fs         # 类型、区分联合、领域模型
│   │   ├── AgentLoop.fs      # 主对话循环
│   │   ├── ToolDispatch.fs   # 读/写分类
│   │   ├── Session.fs        # 会话状态机
│   │   ├── Kanban.fs         # 任务管理
│   │   ├── Memory.fs         # 持久记忆
│   │   └── Workflow.fs       # 状态机 / 编排
│   │
│   ├── Agent.Data/           # C# — EF Core 数据层
│   │   ├── AgentDbContext.cs
│   │   ├── Entities/
│   │   └── Migrations/
│   │
│   ├── tools-python/         # Python — LLM + 领域工具
│   │   ├── server.py         # gRPC 服务
│   │   ├── llm/
│   │   │   ├── provider.py
│   │   │   └── streaming.py
│   │   ├── tools/
│   │   │   ├── web.py
│   │   │   ├── nlp.py
│   │   │   └── logistics.py
│   │   └── pyproject.toml
│   │
│   └── sandbox-go/           # Go — 工具执行沙箱
│       ├── main.go
│       ├── container.go
│       └── limits.go
│
├── deploy/
│   ├── docker-compose.yaml   # 本地开发
│   └── k8s/                  # 生产部署
│
└── my-agent-platform.sln
```

### 8.5 语言边界原则

**语言边界应该匹配关注点边界**：

- **F# 拥有**：做什么（领域逻辑、决策、状态）
- **C# 拥有**：如何服务（HTTP、WebSocket、数据库、认证）
- **Python 拥有**：如何调 AI（LLM SDK、NLP、领域库）
- **Go 拥有**：如何隔离（沙箱、容器管理）

**反模式**：不要让语言边界切割领域概念。所有 kanban 逻辑都应在 F# 中，不要在 F# 状态机和 Python 任务运行器之间拆分。

---

## 9. 平台基础设施领域模型

### 9.1 Session 模型

```sql
CREATE TABLE sessions (
  id TEXT PRIMARY KEY,
  parent_id TEXT REFERENCES sessions(id),
  profile TEXT NOT NULL DEFAULT 'default',
  title TEXT,
  source TEXT,  -- cli, api, telegram, cron
  status TEXT NOT NULL DEFAULT 'active',
  metadata TEXT,  -- JSON
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE messages (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  session_id TEXT NOT NULL REFERENCES sessions(id),
  role TEXT NOT NULL,  -- user, assistant, tool, system
  content TEXT,
  name TEXT,  -- tool name for role=tool
  tool_call_id TEXT,
  tool_calls TEXT,  -- JSON array
  tokens_in INTEGER,
  tokens_out INTEGER,
  created_at TEXT NOT NULL
);

CREATE INDEX idx_messages_session ON messages(session_id, id);
CREATE VIRTUAL TABLE messages_fts USING fts5(content, content=messages, content_rowid=id);
```

### 9.2 Profile 模型

Profile = 隔离目录，不是数据库表。

```
~/.myagent/                     # 默认 profile
  config.yaml
  memories/
  skills/
  sessions.db
  
~/.myagent/profiles/
  logistics-ops/                # 命名 profile
    config.yaml
    memories/
    skills/
    sessions.db
```

```python
@dataclass
class Profile:
    name: str
    home: Path
    
    @property
    def config_path(self) -> Path: return self.home / "config.yaml"
    
    @property
    def db_path(self) -> Path: return self.home / "sessions.db"
```

### 9.3 Kanban 模型

```sql
CREATE TABLE tasks (
  id TEXT PRIMARY KEY,
  board TEXT NOT NULL DEFAULT 'default',
  title TEXT NOT NULL,
  body TEXT,
  status TEXT NOT NULL DEFAULT 'todo',
  assignee TEXT,
  claim_lock TEXT,  -- CAS 锁
  claim_expires INTEGER,
  created_by TEXT,
  summary TEXT,
  metadata TEXT
);

CREATE TABLE task_links (
  parent_id TEXT REFERENCES tasks(id),
  child_id TEXT REFERENCES tasks(id),
  PRIMARY KEY (parent_id, child_id)
);

CREATE TABLE task_comments (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  task_id TEXT REFERENCES tasks(id),
  author TEXT,
  content TEXT,
  created_at TEXT
);

CREATE TABLE task_events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  task_id TEXT REFERENCES tasks(id),
  event_type TEXT,
  data TEXT,  -- JSON
  created_at TEXT
);
```

**CAS 任务认领**：
```python
def claim_task(task_id, worker_id, ttl=600):
    db.execute("""
        UPDATE tasks 
        SET claim_lock = ?, claim_expires = ?, status = 'running'
        WHERE id = ? AND (claim_lock IS NULL OR claim_expires < ?)
    """, [worker_id, now + ttl, task_id, now])
    return db.changes > 0
```

---

## 10. 总结

### 关键洞察

1. **平台层应该无聊且小**。Session = SQLite。Profile = 目录。Kanban = CAS 锁表。复杂性属于领域工具。

2. **正确的表示取决于消费者**。消费者是代码 → 用 SQL/ORM。消费者是 LLM → 用 markdown/自然语言。

3. **语言边界 = 关注点边界**。不要让语言边界切割领域概念。

4. **从 SQLite-per-user 开始**。比你想象的走得更远。只有当付费用户证明基础设施复杂性合理时才迁移到 Postgres。

5. **过早扩展杀死的项目比 SQLite 限制多得多**。
