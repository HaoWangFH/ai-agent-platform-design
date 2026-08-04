# F# Agent 平台：特性路线图与实现任务清单 (Feature Roadmap & Tasks)

本文档概述了 AI Agent 平台 F# 实现 (`implementations/fsharp`) 下两个重大架构特性的设计、实现计划及分析评估：

1. **纯函数式 `RunAsync` 架构**（通过显式状态传递消除实例级 `mutable` 可变状态）
2. **`TaskSeq` LLM 流式处理适配器**（利用 `FSharp.Control.TaskSeq` 实现实时 Token 与工具增量流式传输）

---

## 📋 任务主清单 (Task Master Checklist)

### 任务 1：纯函数式 `RunAsync` 架构 (Pure Functional RunAsync)
- [ ] **1.1 类型与领域层重构**：核论 `Types.fs` 中 `AgentSessionState` 与 `TurnResult` 的纯状态签名。
- [ ] **1.2 创建 `AgentRunner.fs` 模块**：实现返回 `Async<TurnResult * AgentSessionState>` 的纯 `runTurnAsync` 函数。
- [ ] **1.3 重构 `Agent.fs`**：废弃 `Agent` 类包装器内部的 `mutable sessionState` 或提供纯静态入口。
- [ ] **1.4 应用外壳层集成 (`Program.fs`)**：更新 REPL 交互循环，使其在递归回合间显式传递 `AgentSessionState`。
- [ ] **1.5 单元与规范测试迁移**：更新 Expecto 测试套件（`AgentPipelineTests.fs`、`SequentialToolWorkflowSpec.fs`），对纯 `(result, updatedSession)` 元组进行断言。

### 任务 2：`TaskSeq` LLM 流式适配器 (TaskSeq Streaming)
- [ ] **2.1 包依赖添加**：在 `Skight.AgentPlatform.FSharp.fsproj` 中添加 `FSharp.Control.TaskSeq` 包引用。
- [ ] **2.2 领域类型扩展**：在 `Types.fs` 中添加 `StreamChunk` 可区分联合（`TextDelta`、`ToolCallDelta`、`StreamCompleted`）。
- [ ] **2.3 防腐层实现 (`SdkAdapter.fs`)**：实现将 `StreamingChatCompletionsUpdate` 映射为 `ITaskSeq<StreamChunk>` 的 `streamLlmResponse`。
- [ ] **2.4 流式聚合逻辑**：在 `AgentPipeline.fs` 中利用 `TaskSeq.foldAsync` 拼接分片到达的工具调用参数片段。
- [ ] **2.5 程序与测试集成**：在 `Program.fs` 中增加实时流式输出，并编写 Expecto 流式规范测试。

---

## 📐 详细设计、分析与实现方案

### 特性 1：纯函数式 `RunAsync` 架构

#### 1. 分析与评估结果

##### 复杂性与可读性分析
- **核心领域复杂性：** **降低。** 消除实例 `mutable` 字段移除了隐藏的状态追踪，核心领域逻辑实现 100% 参照透明。
- **可读性：** **提升。** 每个函数均明确声明其输入与输出 ($\text{SessionState} \to \text{Input} \to \text{Async}(\text{TurnResult} \times \text{SessionState})$)。
- **调用端影响：** 调用方（`Program.fs`）显式接收并传递更新后的会话状态进入下一回合。

##### 内存与垃圾回收 (GC) 分析
- **结构共享 (Structural Sharing)：** F# 不可变 Record 与单向链表 (`AgentMessage list`) 复用已有的堆内存节点。追加消息仅创建一个新节点，原有消息内容**无需复制**。
- **内存分配开销：** 每回合返回新的 `AgentSessionState` Record 仅产生极小的 **24 至 48 字节** 浅层指针包装。
- **GC 影响：** 无引用的旧会话包装会在 **Generation 0 (Gen 0) GC** 中在不到 1 微秒内被回收，**无任何可察觉的性能开销**。
- **时间旅行功能：** 存储过去的会话快照（用于撤销/重做或对话分支）成本极低（每快照约 24 字节）。

#### 2. 架构设计与函数签名

```fsharp
namespace Skight.AgentPlatform.FSharp

module AgentRunner =

    /// 纯函数式入口：(State, Input) -> Async<TurnResult * NewSessionState>
    let runTurnAsync 
        (llmCaller: LlmCaller) 
        (executor: ToolExecutor) 
        (config: AgentConfig) 
        (userInput: string) 
        (sessionState: AgentSessionState) 
        : Async<TurnResult * AgentSessionState> =
        async {
            // 1. 纯回合前言迁移
            let turnState, nextSessionState = AgentSession.beginTurn config userInput sessionState
            
            // 2. 纯尾递归 4 阶段循环执行
            let! result = AgentPipeline.runTurnLoop llmCaller executor schemas nameSet turnState
            
            // 3. 纯回合终结迁移
            let finalSessionState = AgentSession.applyTurnResult result nextSessionState
            
            return result, finalSessionState
        }
```

#### 3. 顶层应用外壳实现 (`Program.fs`)

```fsharp
// 带有显式状态传递的纯递归 REPL 循环
let rec chatLoop (agent: Agent) (session: AgentSessionState) = async {
    printf "> "
    let input = Console.ReadLine()
    if not (String.IsNullOrEmpty input) && input <> "exit" then
        // 显式在纯运行器间传递会话状态
        let! result, updatedSession = agent.RunPureAsync(input, session)
        return! chatLoop agent updatedSession
}
```

---

### 特性 2：`TaskSeq` LLM 流式处理适配器

#### 1. 分析与评估结果

##### Hermes Agent 功能对齐分析
- **Hermes Agent 原生行为：** 在 Python `conversation_loop.py` 中，Hermes 默认使用 `stream=True`，实时向终端流式输出 Token，同时动态累加工具调用增量。
- **当前参考平台状态：** 目前所有参考实现均使用非流式补全 API (`GetChatCompletionsAsync`)。引入 `TaskSeq` 可实现与 Hermes 流式功能的 **100% 对齐**。

##### 复杂性与可读性分析
- **复杂性：** **增加。** 流式处理需要管理块增量聚合（工具 ID、名称及 JSON 参数分片跨多个数据包到达）。
- **防腐层隔离：** 通过将 SDK 流封装在 `FSharp.Control.TaskSeq` 中，分片拼接复杂性被隔离在**防腐层 (ACL)** 内部，保持 `AgentPipeline.fs` 的干净整洁。
- **用户体验 (UX)：** 为用户提供即时的逐 Token 实时打字机输出。

#### 2. 架构设计与函数签名

##### A. 领域流数据块 DU (`Types.fs`)
```fsharp
type StreamChunk =
    | TextDelta of Content: string
    | ToolCallDelta of Index: int * Id: ToolCallId option * Name: ToolName option * ArgsFragment: string
    | StreamCompleted of FinishReason: string
```

##### B. 防腐层适配器 (`SdkAdapter.fs`)
```fsharp
namespace Skight.AgentPlatform.FSharp

open FSharp.Control // FSharp.Control.TaskSeq
open Azure.AI.OpenAI

module SdkAdapter =

    /// 将 C# SDK IAsyncEnumerable 流映射为纯 F# ITaskSeq<StreamChunk>
    let streamLlmResponse 
        (client: OpenAIClient) 
        (config: AgentConfig) 
        (schemas: ToolSchema list) 
        (messages: AgentMessage list) 
        : ITaskSeq<StreamChunk> =
        taskSeq {
            let requestMessages = messages |> List.map toChatRequestMessage
            let reqOptions = ChatCompletionsOptions(config.Model, requestMessages)
            
            let! response = client.GetChatCompletionsStreamingAsync(reqOptions)
            
            for choiceUpdate in response do
                if not (isNull choiceUpdate.ContentUpdate) && choiceUpdate.ContentUpdate.Length > 0 then
                    yield TextDelta choiceUpdate.ContentUpdate
                    
                if not (isNull choiceUpdate.ToolCallUpdate) then
                    let tc = choiceUpdate.ToolCallUpdate
                    let idOpt = if String.IsNullOrEmpty tc.Id then None else ToolCallId.create tc.Id |> Option.ofResult
                    let nameOpt = if String.IsNullOrEmpty tc.Name then None else ToolName.create tc.Name |> Option.ofResult
                    yield ToolCallDelta (tc.ToolCallIndex, idOpt, nameOpt, tc.Arguments)
        }
```

##### C. 函数式流聚合器 (`AgentPipeline.fs`)
```fsharp
/// 将传入的流数据块累加合并为完整的 LlmTurnResponse
let aggregateStream (stream: ITaskSeq<StreamChunk>) (onTextChunk: string -> unit) : Async<LlmTurnResponse> =
    async {
        let! (textBuffer, toolCallMap) =
            stream
            |> TaskSeq.foldAsync (fun (textAcc, toolMap) chunk ->
                async {
                    match chunk with
                    | TextDelta text ->
                        onTextChunk text // 实时 UI 回调
                        return (textAcc + text, toolMap)
                    | ToolCallDelta (idx, idOpt, nameOpt, argsFragment) ->
                        let updatedMap = updateToolCallAccumulator idx idOpt nameOpt argsFragment toolMap
                        return (textAcc, updatedMap)
                    | StreamCompleted _ ->
                        return (textAcc, toolMap)
                }
            ) ("", Map.empty)

        return {
            Content = textBuffer
            ToolCalls = toolCallMap |> Map.toList |> List.map snd
        }
    }
```

---

## 🎯 验收标准 (Acceptance Criteria)

1. **零 `mutable` 实例状态：** 核心 Agent 逻辑与回合运行器在没有任何 `mutable` 字段的情况下运行。
2. **确定性状态传递：** `runTurnAsync` 返回 `(TurnResult * AgentSessionState)`，经由 Expecto 单元测试验证。
3. **TaskSeq 实时流式传输：** `TaskSeq` 在 `Program.fs` 中向控制台实时流式输出 Token，并准确累加工具调用参数增量。
4. **构建与测试通过：** `dotnet test implementations/fsharp/Skight.AgentPlatform.FSharp.sln` 100% 通过。
