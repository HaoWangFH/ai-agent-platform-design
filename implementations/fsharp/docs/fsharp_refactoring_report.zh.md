# F# 代码库重构报告

本文档概述了 AI Agent 平台 F# 实现 (`implementations/fsharp`) 的架构改进和重构。此次重构的目标是深度整合函数式编程惯用法、领域驱动设计 (DDD) 原则以及 F# 最佳实践，使核心领域逻辑远离面向对象 (C#) 的 SDK 范式。

## 1. 让非法状态不可表示 (Making Illegal States Unrepresentable)

以前，`TurnOutcome.Failed` 状态通过一个可选的错误消息字段 (`ErrorMessage: string option`) 来追踪失败原因。这种松散的耦合导致错误原因和错误消息可能会不同步或部分缺失。

**改进:**
*   移除了不一致的 `ErrorMessage: string option` 字段。
*   将错误详细信息内聚地绑定到 `FailureReason` 可区分联合 (DU) 中。

```fsharp
// 重构前
type FailureReason = BudgetExhausted | ApiError of string | NoResponse of string
type TurnOutcome = Failed of Reason: FailureReason * ErrorMessage: string option

// 重构后
type FailureReason = BudgetExhausted of string | ApiError of string | NoResponse of string
type TurnOutcome = Failed of Reason: FailureReason
```

## 2. 解析而非验证 (Parse, Don't Validate / 智能构造函数)

核心领域层之前传递原始字符串 (primitive strings) 来标识工具调用 ID 和工具名称等实体。这些原始类型本质上是不安全的，因为它们需要在系统的各个入口点不断地进行重新验证。

**改进:**
*   将关键的原始类型封装到单例强类型的可区分联合 (`ToolCallId` 和 `ToolName`) 中。
*   使用 `private` 私有构造函数限制直接实例化。
*   暴露模块级别的 `create` 函数作为智能构造函数 (Smart Constructors)。这些构造函数返回 `Result<T, string>`，作为数据有效性的唯一守门员。

```fsharp
type ToolName = private ToolName of string

module ToolName =
    let create (name: string) =
        if System.String.IsNullOrWhiteSpace(name) then Error "ToolName cannot be empty"
        else Ok (ToolName name)
        
    let value (ToolName name) = name
```

这种严格类型检查随后贯穿了核心系统模型，包括 `ToolCall`、`AgentMessage` 和 `ToolDefinition`。

## 3. 防腐层 (ACL) 增强

为了防止 `Azure.AI.OpenAI` SDK 的可变性和以 C# 为中心的设计泄漏到纯函数式的核心中，API 响应与领域模型之间的边界被进一步收紧。

**改进:**
*   用清晰的 F# 活动模式 (Active Patterns) 替换了内联的手动类型检查和向下转型 (`:?`)。
*   活动模式 `(|FunctionToolCall|_|)` 现在优雅地解包多态的 SDK 类型，只暴露领域所需的内容。

```fsharp
let (|FunctionToolCall|_|) (tc: ChatCompletionsToolCall) =
    match tc with
    | :? ChatCompletionsFunctionToolCall as fnCall -> Some fnCall
    | _ -> None
```
该活动模式现在被用于将 SDK 的有效载荷安全地映射到基于 `Result` 的智能构造函数，在它们进入领域逻辑之前将无效响应直接清除。

## 4. 测试套件对齐

向更严格类型的过渡要求更新纯流水线规范测试和顺序工作流集成测试。

**改进:**
*   更新了 `AgentPipelineTests.fs`，以针对全新且简化的 `FailureReason` 结构进行断言。
*   重构了 `SequentialToolWorkflowSpec.fs` 以适应 `ToolCallId` 和 `ToolName`。被测试模拟的 LLM 组件和执行器现在安全地使用智能构造函数来注入其假状态。

---

## 5. 函数式编程规范对齐审查 (Guideline Compliance Review)

参照 [AI Agent 移植 F# 函数式编程指南](../../../.gemini/config/skills/fsharp-porting/SKILL.md) 对代码库进行了系统性审查，涵盖全部 6 条架构准则。

### 评分卡 (Scorecard)

| # | 规范准则 (Guideline) | 状态 | 评分 |
|---|-----------|--------|-------|
| 1 | 让非法状态不可表示 (Make Illegal States Unrepresentable) | ✅ 完全对齐 | **A** |
| 2 | 解析而非验证 (Parse, Don't Validate / Smart Constructors) | ✅ 完全对齐 | **A** |
| 3 | 构建防腐层隔离 OO SDK (Anti-Corruption Layer) | ✅ 完全对齐 | **A** |
| 4 | 纯函数与只追加历史 (Pure Functions & Append-Only History) | ⚠️ 部分对齐 | **B** |
| 5 | 函数式流式处理 (TaskSeq Functional Streaming) | 🔘 不适用 | **—** |
| 6 | 函数式测试实践 (Functional Testing Practices) | ✅ 完全对齐 | **A** |

**综合评分：A-** — 整体高度符合函数式范式，存在少量细节可继续优化。

### 5.1 让非法状态不可表示 — 评分 A

`TurnOutcome` 可区分联合 (DU) 完全替代了多个独立的布尔标志。在类型系统层面上，不可能构造出同时处于 `Completed` 和 `Failed` 状态的 `TurnResult`：

```fsharp
type TurnOutcome =
    | Completed of FinalResponse: string
    | Interrupted
    | Failed of Reason: FailureReason
```

过去包含 `Completed: bool`、`Failed: bool`、`Interrupted: bool` 和 `Error: string option` 的冗余 Record 结构已被彻底废弃。

### 5.2 解析而非验证 — 评分 A

`ToolCallId` 和 `ToolName` 使用私有构造函数配合返回 `Result` 的 `create` 智能构造函数。这些类型在整个领域层中贯穿使用 — `ToolCall` 记录使用强类型包装而非原始字符串。`ToolRegistry` 在注册边界接收原始字符串并使用 `ToolName.create` 进行校验。

**未来优化方向：** `AgentConfig.Model` 目前仍为原始 `string`。若模型标识进入核心领域逻辑并可能因无效值触发运行时错误，可进一步提炼为 `type ModelId = ModelId of string`。

### 5.3 防腐层 (ACL) 隔离 — 评分 A

防腐层在 `Agent` 类中被清晰隔离，具备明确的入站/出站数据映射：

| 方向 | 函数/模式 | 映射关系 |
|-----------|----------|----------------|
| SDK → Domain | `toDomainResponse` | `ChatResponseMessage` → `LlmTurnResponse` |
| Domain → SDK | `toChatRequestMessage` | `AgentMessage` → `ChatRequestMessage` |
| Domain → SDK | `toFunctionDefinition` | `ToolSchema` → `FunctionDefinition` |
| SDK 解包 | `(|FunctionToolCall|_|)` | 用于安全解包 SDK 类型的活动模式 (Active Pattern) |

核心 `AgentPipeline` 模块完全运行在 `AgentMessage`、`TurnState`、`ToolCall`、`ToolName`、`ToolCallId` 等纯领域类型上 — 没有任何 SDK 类型泄漏到管道内。`LlmCaller` 的类型签名为 `ToolSchema list -> AgentMessage list -> Async<Result<LlmTurnResponse, LlmError>>`。

**未来优化方向：** 目前 ACL 函数作为 `Agent` 类的私有成员存在。可考虑提炼为顶层模块 `module SdkAdapter`，使边界更加显式且可独立单元测试。

### 5.4 纯函数与只追加历史 — 评分 B

**优势：**
*   `AgentPipeline` 模块完全由操作不可变 `TurnState` 的纯函数组成，每个步骤均返回新状态，绝不进行原地修改。
*   `AgentSession` 模块将会话状态迁移建模为纯函数：`beginTurn`、`applyTurnResult`、`requestInterrupt` 均返回新状态。

**改进空间：** 最外层的 `Agent` 类包装器中仍包含 `mutable sessionState`（第 288 行），并在 `RunAsync` 中对其进行了赋值修改（第 300、311 行）。虽然该可变修改被严格限定在 I/O 外壳层，纯 `AgentPipeline` 模块并未受到影响，但从纯函数式角度看，仍有提升空间。

**优化方案：** 让 `RunAsync` 返回 `TurnResult * AgentSessionState`，由 `Program.fs` 负责会话状态的传递与持有。

### 5.5 函数式流式处理 (TaskSeq) — 不适用

目前尚未实现流式响应（所有 LLM 调用均使用非流式 `GetChatCompletionsAsync`）。后续引入流式输出时，应使用 `FSharp.Control.TaskSeq` 并将 SDK 块在 ACL 边界处解包映射为纯 DU 类型。

### 5.6 函数式测试实践 — 评分 A

测试套件充分体现了 F# 函数式测试惯用法：

**对 `TurnOutcome` DU 进行模式匹配**（替代 `Expect.isTrue result.Completed`）：
```fsharp
match AgentPipeline.checkInterrupt interruptedState with
| Exit { Outcome = TurnOutcome.Interrupted; ApiCalls = apiCalls } ->
    Expect.equal apiCalls 0 "Expected zero API calls on interrupt"
```

**使用匿名记录 (Anonymous Records) 进行扁平化断言**（利用编译器自动推导差异）：
```fsharp
let actual = {| Reason = reason |}
let expected = {| Reason = FailureReason.BudgetExhausted "Budget exhausted" |}
Expect.equal actual expected "Expected budget failure outcome"
```

**消息历史的结构化解构匹配**（无需 `:?>` 强制类型转换）：
```fsharp
match result.Messages.[2], result.Messages.[3], ... with
| AssistantMessage (_, firstCalls), ToolMessage (firstCallId, firstResult), ... ->
    let actual = {| FirstToolName = ...; ... |}
```

### 5.7 待优化项汇总

| 优先级 | 事项 | 涉及文件 | 工作量 |
|----------|------|------|--------|
| 低 | 将 ACL 函数提取为 `module SdkAdapter` | `Agent.fs` | 较小 |
| 低 | 为 `AgentConfig.Model` 增加 `ModelId` 智能构造函数 | `Types.fs` | 微小 |
| 中 | `RunAsync` 返回 `TurnResult * AgentSessionState` 以消除 `Agent` 类中的 `mutable sessionState` | `Agent.fs` | 中等 |

## 结论
通过利用 F# 类型系统在编译时阻止无效的逻辑路径，Agent 平台的 F# 实现现在明显更安全。通过将验证推至应用程序边缘 (防腐层 ACL 和智能构造函数)，内部纯函数可以毫无防御性编程开销地运行。规范对齐审查证实代码库与各项函数式架构准则高度符合 (A-)，仅存少量细节优化项。
