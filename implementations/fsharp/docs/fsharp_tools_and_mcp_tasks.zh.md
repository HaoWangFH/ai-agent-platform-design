# F# Agent 平台：核心工具与 MCP 集成任务清单与设计 (Tools & MCP Tasks)

本文档概述了 AI Agent 平台 F# 实现 (`implementations/fsharp`) 下另外两个重大工具执行特性的架构设计、安全守卫、使用工作流、3 层测试策略及实现计划：

1. **任务 3：核心工具与安全执行环境**（沙箱路径守卫、子进程终端执行、进程超时管理、审批守卫、Diff 编辑器、输出截断限制）
2. **任务 4：MCP (Model Context Protocol) 服务端集成**（`stdio` / `SSE` JSON-RPC 2.0 客户端、工具 Schema 转换与动态注册）

---

## 📋 任务主清单 (Task Master Checklist)

### 任务 3：核心工具与安全执行环境 (Core Tools & Security)
- [ ] **3.1 安全与沙箱模块 (`ToolSecurity.fs`)**：实现 `validateSandboxPath` 路径跨越守卫（基于工作区根目录的 `Path.GetFullPath` 校验）。
- [ ] **3.2 文件操作工具集 (`FileTools.fs`)**：实现 `read_file`、`write_file` 和 `edit_file`（查找替换 / 统一 Diff 块补丁编辑器）。
- [ ] **3.3 输出限制与截断守卫**：实现 `truncateOutput`（将 stdout/stderr 限制在 100 KB / 500 行以内并带有截断标记）。
- [ ] **3.4 进程执行引擎 (`TerminalTool.fs`)**：使用 `System.Diagnostics.Process` 实现 `execute_command`，具备 60 秒超时处理与后台进程追踪。
- [ ] **3.5 交互式审批守卫 (`ApprovalGuard.fs`)**：实现确认钩子，在执行高风险命令或文件修改前提示用户确认。
- [ ] **3.6 单元与集成测试套件**：编写覆盖路径安全、进程超时及文件操作的 3 层 Expecto & MSpec 测试套件。

### 任务 4：MCP (Model Context Protocol) 服务端集成 (MCP Integration)
- [ ] **4.1 JSON-RPC 2.0 协议适配器 (`McpProtocol.fs`)**：实现适用于 `stdio` IPC 的 JSON-RPC 2.0 请求/响应序列化器。
- [ ] **4.2 MCP 客户端管理器 (`McpClient.fs`)**：实现管理子进程生命周期（`npx` / 可执行 MCP 服务端）的 `McpClient`。
- [ ] **4.3 Schema 转换器 (`McpSchemaTranslator.fs`)**：将 MCP `tools/list` 响应清单转换为 `ToolSchema` 记录。
- [ ] **4.4 注册表动态自动发现**：将 MCP 工具动态绑定到 `ToolRegistry.fs` 中。
- [ ] **4.5 MCP 规范测试套件**：利用 Mock stdio MCP 服务端子进程实现 Expecto 测试。

---

## 📐 详细设计、安全层与实现方案

### 特性 3：核心工具与安全执行环境

#### 1. 5 大安全与控制层 (5 Security & Control Layers)

为了安全地执行终端命令和修改文件而不崩溃 Agent 循环或损坏宿主环境，系统强制实施 5 层安全防护：

```
┌─────────────────────────────────────────────────────────────┐
│ 1. 沙箱路径安全守卫 (Sandbox Path Security Guard)           │
├─────────────────────────────────────────────────────────────┤
│ 2. 命令审批守卫 (Interactive User Command Approval)         │
├─────────────────────────────────────────────────────────────┤
│ 3. 进程超时与异步生命周期管理器 (Process Timeout Manager)   │
├─────────────────────────────────────────────────────────────┤
│ 4. 统一 Diff 补丁文件编辑器 (Unified Patch / Diff Editor)    │
├─────────────────────────────────────────────────────────────┤
│ 5. 输出截断与 Token 溢出保护 (Output Truncation Guard)       │
└─────────────────────────────────────────────────────────────┘
```

#### 2. 实现签名与代码示例

##### A. 沙箱路径安全 (`ToolSecurity.fs`)
```fsharp
namespace Skight.AgentPlatform.FSharp

open System
open System.IO

module ToolSecurity =

    /// 校验路径是否严格位于工作区沙箱内部
    let validateSandboxPath (workspaceRoot: string) (targetPath: string) : Result<string, string> =
        try
            let rootCanonical = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar)
            let targetCanonical = Path.GetFullPath(Path.Combine(workspaceRoot, targetPath))
            
            if targetCanonical.StartsWith(rootCanonical, StringComparison.OrdinalIgnoreCase) then
                Ok targetCanonical
            else
                Error (sprintf "Access denied: Path '%s' is outside workspace sandbox." targetPath)
        with ex ->
            Error (sprintf "Invalid path '%s': %s" targetPath ex.Message)

    /// 截断过大的工具输出以防止 Token 溢出
    let truncateOutput (maxBytes: int) (output: string) : string =
        if String.IsNullOrEmpty output || output.Length <= maxBytes then
            output
        else
            let truncated = output.Substring(0, maxBytes)
            sprintf "%s\n\n[Output truncated: %d characters hidden to protect context window]" truncated (output.Length - maxBytes)
```

##### B. 终端子进程引擎 (`TerminalTool.fs`)
```fsharp
namespace Skight.AgentPlatform.FSharp

open System
open System.Diagnostics
open System.Threading

module TerminalTool =

    let executeCommandAsync (timeoutMs: int) (maxOutputBytes: int) (cmdStr: string) : Async<string> =
        async {
            use proc = new Process()
            let isWindows = Environment.OSVersion.Platform = PlatformID.Win32NT
            proc.StartInfo.FileName <- if isWindows then "cmd.exe" else "/bin/bash"
            proc.StartInfo.Arguments <- if isWindows then sprintf "/c \"%s\"" cmdStr else sprintf "-c \"%s\"" cmdStr
            proc.StartInfo.RedirectStandardOutput <- true
            proc.StartInfo.RedirectStandardError <- true
            proc.StartInfo.UseShellExecute <- false
            proc.StartInfo.CreateNoWindow <- true

            use cts = new CancellationTokenSource(timeoutMs)
            try
                proc.Start() |> ignore
                let! stdoutTask = proc.StandardOutput.ReadToEndAsync() |> Async.AwaitTask
                let! stderrTask = proc.StandardError.ReadToEndAsync() |> Async.AwaitTask
                let! exited = proc.WaitForExitAsync(cts.Token) |> Async.AwaitTask

                let combined = 
                    if String.IsNullOrEmpty stderrTask then stdoutTask
                    else sprintf "%s\n[Stderr]: %s" stdoutTask stderrTask

                return ToolSecurity.truncateOutput maxOutputBytes combined
            with
            | :? OperationCanceledException ->
                try proc.Kill(true) with _ -> ()
                return sprintf "Error: Command '%s' timed out after %d ms and was terminated." cmdStr timeoutMs
            | ex ->
                return sprintf "Error executing command: %s" ex.Message
        }
```

---

### 特性 4：MCP (Model Context Protocol) 服务端集成

#### 1. Hermes 对齐分析 (`tools/mcp_tool.py`)
- **Hermes 模式**：Hermes Agent 通过 `stdio`（标准输入/输出 JSON-RPC 2.0）或 `SSE` HTTP 服务端连接外部 MCP 服务端。
- **协议握手流程**：
  1. 启动 MCP 服务端子进程（例如 `npx -y @modelcontextprotocol/server-sqlite`）。
  2. 发送 `tools/list` JSON-RPC 请求以发现外部工具。
  3. 将 MCP 工具 Schema 转换为 OpenAI JSON Function Schema。
  4. 当 LLM 调用 MCP 工具时，路由 `tools/call` JSON-RPC 请求。

#### 2. 架构对比：工具调用 (Tool Calling) vs. MCP 协议 (Model Context Protocol)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          LLM Provider (OpenAI / Anthropic)                  │
└─────────────────────────────────────▲───────────────────────────────────────┘
                                      │
                                  Tool Calling
                             (Model API Protocol)
                                      │
┌─────────────────────────────────────▼───────────────────────────────────────┐
│                        Your AI Agent Platform                               │
│                         (Acts as MCP Client)                                │
└───────────────┬─────────────────────────────────────────────┬───────────────┘
                │                                             │
         Native Local Tools                               MCP Protocol
    (In-Process F# / C# Functions)                       (JSON-RPC 2.0)
                │                                             │
    ┌───────────┴───────────┐                     ┌───────────▼───────────┐
    │  read_file, execute   │                     │   External MCP Server │
    │  (Compiled into App)  │                     │   (Separate Process   │
    └───────────────────────┘                     │    e.g. SQLite, GitHub)│
                                                  └───────────────────────┘
```

| 特性 / 维度 | 工具调用 (Tool Calling / Function Calling) | MCP 协议 (Model Context Protocol) |
|---|---|---|
| **本质定义** | 模型提供商（OpenAI、Anthropic、Gemini）内置的 **LLM API 功能机制**。 | 由 Anthropic 创制的连接 Agent 与外部工具/数据的 **开放客户端-服务端集成标准**。 |
| **作用层级** | 作用于 **Agent** 与 **LLM API** 之间（基于 HTTP REST）。 | 作用于 **Agent (MCP Client)** 与 **外部工具服务 (MCP Server)** 之间（基于 `stdio` IPC 或 `SSE` HTTP）。 |
| **执行位置** | **进程内 (In-Process)** 执行，直接在应用程序代码中运行。 | **进程外 (Out-of-Process)** 执行，在独立的 MCP 服务端进程或远程服务中运行。 |
| **复用性** | 应用特定代码。为另一个 Agent 重用工具需要重新编写代码。 | **通用插件生态**。任何 MCP 服务端（如 PostgreSQL、GitHub）可无需修改直接插入**任何 AI Agent**。 |
| **能力范畴** | 仅支持工具 (函数名称 + JSON 参数)。 | **工具 + 资源 + 提示词**：支持暴露文件树 (`resources/list`)、预制提示词 (`prompts/list`) 和可执行工具 (`tools/list`)。 |

#### 3. F# MCP 客户端架构与代码签名 (`McpAdapter.fs`)

```fsharp
namespace Skight.AgentPlatform.FSharp

open System.Diagnostics
open System.Text.Json

type McpClient(command: string, args: string) =
    let proc = new Process()
    do
        proc.StartInfo.FileName <- command
        proc.StartInfo.Arguments <- args
        proc.StartInfo.RedirectStandardInput <- true
        proc.StartInfo.RedirectStandardOutput <- true
        proc.StartInfo.UseShellExecute <- false
        proc.StartInfo.CreateNoWindow <- true
        proc.Start() |> ignore

    member _.ListToolsAsync() : Async<ToolSchema list> =
        async {
            let reqJson = """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}"""
            do! proc.StandardInput.WriteLineAsync(reqJson) |> Async.AwaitTask
            let! respLine = proc.StandardOutput.ReadLineAsync() |> Async.AwaitTask
            
            // 将 JSON-RPC tools/list 响应解析为 ToolSchema 记录
            use doc = JsonDocument.Parse(respLine)
            let toolsArray = doc.RootElement.GetProperty("result").GetProperty("tools")
            
            return [
                for t in toolsArray.EnumerateArray() do
                    let nameStr = t.GetProperty("name").GetString()
                    match ToolName.create nameStr with
                    | Ok name ->
                        yield {
                            Name = name
                            Description = t.GetProperty("description").GetString()
                            ParametersJson = t.GetProperty("inputSchema").GetRawText()
                        }
                    | Error _ -> ()
            ]
        }

    member _.CallToolAsync(name: ToolName, argsJson: string) : Async<string> =
        async {
            let nameStr = ToolName.value name
            let reqJson = sprintf """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"%s","arguments":%s}}""" nameStr argsJson
            do! proc.StandardInput.WriteLineAsync(reqJson) |> Async.AwaitTask
            let! respLine = proc.StandardOutput.ReadLineAsync() |> Async.AwaitTask
            return respLine
        }
```

---

## 🧪 3 层测试策略与代码示例 (3-Level Testing Strategy)

```
┌─────────────────────────────────────────────────────────────┐
│ 第 1 层: 单元测试 (独立测试工具处理程序与安全守卫)          │
│          - 沙箱路径跨越检查                                 │
│          - 输出截断限制                                     │
├─────────────────────────────────────────────────────────────┤
│ 第 2 层: 模拟循环测试 (Agent 管道 + 真实工具执行)           │
│          - Mock LLM 发出工具调用；真实工具执行              │
│          - 经由 Expecto (F#) 或 MSpec (C#) 验证            │
├─────────────────────────────────────────────────────────────┤
│ 第 3 层: 沙箱集成测试 (临时工作区清理)                       │
│          - 针对 /tmp 中的真实文件和子进程进行测试           │
└─────────────────────────────────────────────────────────────┘
```

### 第 1 层 & 第 2 层：Expecto F# 规范测试 (`ToolExecutionTests.fs`)

```fsharp
namespace Skight.AgentPlatform.FSharp.Tests

open System.IO
open Expecto
open Skight.AgentPlatform.FSharp

module ToolExecutionTests =

    let testWorkspace = Path.Combine(Path.GetTempPath(), "fsharp_tool_sandbox")

    [<Tests>]
    let toolTests =
        testList "F# 工具安全与执行规范测试" [

            // 第 1 层：路径安全守卫测试
            test "沙箱路径安全守卫成功阻止越界访问" {
                Directory.CreateDirectory(testWorkspace) |> ignore
                
                match ToolSecurity.validateSandboxPath testWorkspace "../../etc/passwd" with
                | Error err -> Expect.stringContains err "Access denied" "路径跨越被阻止"
                | Ok _ -> failtest "期望越界访问被阻止"
            }

            // 第 1 层：输出限制守卫测试
            test "TruncateOutput 限制过大的工具输出" {
                let largeText = String.replicate 1000 "LOG_LINE\n"
                let result = ToolSecurity.truncateOutput 100 largeText
                Expect.isTrue (result.Length <= 200) "输出必须被截断"
                Expect.stringContains result "[Output truncated" "包含截断标记"
            }

            // 第 2 层：Agent 循环内的真实工具集成测试
            testAsync "Agent 循环顺序执行真实的 write_file 和 read_file 工具" {
                async {
                    let tempFile = Path.Combine(testWorkspace, "integration_sample.txt")
                    let callCounter = ref 0

                    let mockLlmCaller : LlmCaller =
                        fun _ _ -> async {
                            incr callCounter
                            match !callCounter with
                            | 1 ->
                                let args = sprintf """{"path":"%s","content":"Hello Expecto!"}""" (tempFile.Replace("\\", "/"))
                                return Ok { Content = ""; ToolCalls = [ { Id = ToolCallId.create "c1" |> Result.defaultWith failwith; Name = ToolName.create "write_file" |> Result.defaultWith failwith; ArgumentsJson = args } ] }
                            | 2 ->
                                let args = sprintf """{"path":"%s"}""" (tempFile.Replace("\\", "/"))
                                return Ok { Content = ""; ToolCalls = [ { Id = ToolCallId.create "c2" |> Result.defaultWith failwith; Name = ToolName.create "read_file" |> Result.defaultWith failwith; ArgumentsJson = args } ] }
                            | 3 ->
                                return Ok { Content = "Verified file creation.", ToolCalls = [] }
                            | _ -> return Error (ApiCallFailed "Unexpected LLM call")
                        }

                    let registry = ToolRegistry()
                    registry.Register("write_file", "写入文件", (fun args -> async {
                        use doc = System.Text.Json.JsonDocument.Parse(args)
                        let path = doc.RootElement.GetProperty("path").GetString()
                        let content = doc.RootElement.GetProperty("content").GetString()
                        File.WriteAllText(path, content)
                        return "File written."
                    }), "{}")

                    registry.Register("read_file", "读取文件", (fun args -> async {
                        use doc = System.Text.Json.JsonDocument.Parse(args)
                        let path = doc.RootElement.GetProperty("path").GetString()
                        return File.ReadAllText(path)
                    }), "{}")

                    let initialState : TurnState = {
                        Messages = [ SystemMessage "sys"; UserMessage "Test tools" ]
                        ApiCalls = 0
                        EmptyContentRetries = 0
                        Command = RunTurn
                        Config = { MaxIterations = 5; MaxRetries = 2; ContextWindowLimit = 10; Model = "test-model" }
                    }

                    let! result = AgentPipeline.runTurnLoop mockLlmCaller registry.AsExecutor [] (registry.GetRegisteredNames() |> Set.ofList) initialState

                    match result.Outcome with
                    | TurnOutcome.Completed text ->
                        Expect.equal text "Verified file creation." "最终响应文本匹配"
                        Expect.isTrue (File.Exists(tempFile)) "磁盘上实际存在文件"
                        Expect.equal (File.ReadAllText(tempFile)) "Hello Expecto!" "文件内容匹配"
                    | outcome -> failtestf "Expected completed outcome, got %A" outcome
                }
            }
        ]
```

---

## 🎯 验收标准 (Acceptance Criteria)

1. **沙箱强制校验**：`validateSandboxPath` 100% 阻止路径跨越企图 (`../..`, `C:\Windows`)。
2. **进程超时保护**：超过 60 秒的命令会被干净终结，不会卡死对话回合。
3. **输出上限限制**：超过 100 KB 的工具输出会被自动截断并附带诊断标记。
4. **MCP stdio 对齐**：`McpClient` 连接到 stdio MCP 进程，自动发现 `tools/list` 并路由 `tools/call`。
5. **测试套件 100% 通过**：`dotnet test implementations/fsharp/Skight.AgentPlatform.FSharp.sln` 100% 通过。
