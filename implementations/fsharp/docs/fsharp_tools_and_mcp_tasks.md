# F# Agent Platform: Core Tools & MCP Integration Task List & Design

This document outlines the architectural design, implementation plan, security guards, usage workflows, and 3-level testing strategy for the next two major tool execution features in the F# Agent Platform (`implementations/fsharp`):

1. **Task 3: Core Tools & Safe Execution Environment** (Sandbox Path Guards, Subprocess Terminal Execution, Process Timeouts, Approval Guards, Diff Editors, Output Limits)
2. **Task 4: MCP (Model Context Protocol) Server Integration** (`stdio` / `SSE` JSON-RPC 2.0 Client, Tool Schema Translation & Dynamic Registration)

---

## 📋 Task Master Checklist

### Task 3: Core Tools & Safe Execution Environment
- [ ] **3.1 Security & Sandbox Module (`ToolSecurity.fs`)**: Implement `validatePathInSandbox` path traversal guard (`Path.GetFullPath` check against workspace root).
- [ ] **3.2 File Operations Toolset (`FileTools.fs`)**: Implement `read_file`, `write_file`, and `edit_file` (search-and-replace / diff block patch editor).
- [ ] **3.3 Output Limit & Truncation Guard**: Implement `truncateOutput` (capping stdout/stderr at 100 KB / 500 lines with truncation markers).
- [ ] **3.4 Process Execution Engine (`TerminalTool.fs`)**: Implement `execute_command` using `System.Diagnostics.Process` with 60s timeout handling and background process tracking.
- [ ] **3.5 Interactive Approval Guard (`ApprovalGuard.fs`)**: Implement confirmation hook prompting users before executing high-risk commands or file edits.
- [ ] **3.6 Unit & Integration Test Suite**: Implement 3-level Expecto & MSpec test suite covering path security, process timeouts, and file operations.

### Task 4: MCP (Model Context Protocol) Server Integration
- [ ] **4.1 JSON-RPC 2.0 Protocol Adapter (`McpProtocol.fs`)**: Implement JSON-RPC 2.0 request/response serializers for `stdio` IPC.
- [ ] **4.2 MCP Client Manager (`McpClient.fs`)**: Implement `McpClient` managing subprocess lifecycle (`npx` / executable MCP servers) over stdin/stdout.
- [ ] **4.3 Schema Translator (`McpSchemaTranslator.fs`)**: Translate MCP `tools/list` response manifests into `ToolSchema` records.
- [ ] **4.4 Registry Auto-Discovery**: Bind MCP tools dynamically into `ToolRegistry.fs`.
- [ ] **4.5 MCP Specification Test Suite**: Implement Expecto tests using mock stdio MCP server subprocesses.

---

## 📐 Detailed Design, Security Layers & Implementation Plans

### Feature 3: Core Tools & Safe Execution Environment

#### 1. The 5 Security & Control Layers

To execute terminal commands and modify files safely without crashing the agent loop or damaging the host environment, the system enforces 5 security layers:

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Sandbox Path Security Guard (Path Traversal Protection)  │
├─────────────────────────────────────────────────────────────┤
│ 2. Command Approval Guard (Interactive User Confirmation)   │
├─────────────────────────────────────────────────────────────┤
│ 3. Process Timeout & Async Lifecycle Manager                │
├─────────────────────────────────────────────────────────────┤
│ 4. Unified Patch / Diff File Editor (Prevent Destruction)   │
├─────────────────────────────────────────────────────────────┤
│ 5. Output Truncation & Token Spill Protection               │
└─────────────────────────────────────────────────────────────┘
```

#### 2. Implementation Signatures & Code Samples

##### A. Sandbox Path Security (`ToolSecurity.fs`)
```fsharp
namespace Skight.AgentPlatform.FSharp

open System
open System.IO

module ToolSecurity =

    /// Validates that path is inside the workspace sandbox
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

    /// Truncates large tool outputs to prevent token spill
    let truncateOutput (maxBytes: int) (output: string) : string =
        if String.IsNullOrEmpty output || output.Length <= maxBytes then
            output
        else
            let truncated = output.Substring(0, maxBytes)
            sprintf "%s\n\n[Output truncated: %d characters hidden to protect context window]" truncated (output.Length - maxBytes)
```

##### B. Terminal Subprocess Engine (`TerminalTool.fs`)
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

### Feature 4: MCP (Model Context Protocol) Server Integration

#### 1. Hermes Parity Analysis (`tools/mcp_tool.py`)
- **Hermes Pattern**: Hermes Agent connects to external MCP servers via `stdio` (stdin/stdout JSON-RPC 2.0) or `SSE` HTTP servers.
- **Protocol Handshake**:
  1. Spawns MCP server process (e.g. `npx -y @modelcontextprotocol/server-sqlite`).
  2. Sends `tools/list` JSON-RPC request to discover external tools.
  3. Converts MCP tool schemas into OpenAI JSON Function Schemas.
  4. Routes `tools/call` JSON-RPC requests when the LLM invokes an MCP tool.

#### 2. F# MCP Client Architecture & Code Signatures (`McpAdapter.fs`)

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
            
            // Parse JSON-RPC tools/list response into ToolSchema records
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

## 🧪 3-Level Testing Strategy & Code Examples

```
┌─────────────────────────────────────────────────────────────┐
│ Level 1: Unit Tests (Tool Handlers & Guards in Isolation)  │
│          - Sandbox path traversal checks                    │
│          - Output truncation limits                         │
├─────────────────────────────────────────────────────────────┤
│ Level 2: Mocked Loop Tests (Agent Pipeline + Real Tools)   │
│          - Mock LLM issues tool calls; real tools execute    │
│          - Verified via Expecto (F#) or MSpec (C#)          │
├─────────────────────────────────────────────────────────────┤
│ Level 3: Sandbox Integration Tests (Temp Workspace Cleanup)│
│          - Test against real files & subprocesses in /tmp   │
└─────────────────────────────────────────────────────────────┘
```

### Level 1 & 2: Expecto F# Specification Tests (`ToolExecutionTests.fs`)

```fsharp
namespace Skight.AgentPlatform.FSharp.Tests

open System.IO
open Expecto
open Skight.AgentPlatform.FSharp

module ToolExecutionTests =

    let testWorkspace = Path.Combine(Path.GetTempPath(), "fsharp_tool_sandbox")

    [<Tests>]
    let toolTests =
        testList "F# Tool Security & Execution Specification Tests" [

            // Level 1: Path Security Guard
            test "Path Security Guard blocks access outside sandbox" {
                Directory.CreateDirectory(testWorkspace) |> ignore
                
                match ToolSecurity.validateSandboxPath testWorkspace "../../etc/passwd" with
                | Error err -> Expect.stringContains err "Access denied" "Traversal blocked"
                | Ok _ -> failtest "Expected traversal attempt to be blocked"
            }

            // Level 1: Output Limit Guard
            test "TruncateOutput caps large tool output" {
                let largeText = String.replicate 1000 "LOG_LINE\n"
                let result = ToolSecurity.truncateOutput 100 largeText
                Expect.isTrue (result.Length <= 200) "Output must be truncated"
                Expect.stringContains result "[Output truncated" "Truncation marker present"
            }

            // Level 2: Real Tool Integration inside Agent Loop
            testAsync "Agent loop executes real write_file and read_file tools sequentially" {
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
                    registry.Register("write_file", "Writes file", (fun args -> async {
                        use doc = System.Text.Json.JsonDocument.Parse(args)
                        let path = doc.RootElement.GetProperty("path").GetString()
                        let content = doc.RootElement.GetProperty("content").GetString()
                        File.WriteAllText(path, content)
                        return "File written."
                    }), "{}")

                    registry.Register("read_file", "Reads file", (fun args -> async {
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
                        Expect.equal text "Verified file creation." "Final response text"
                        Expect.isTrue (File.Exists(tempFile)) "File written on disk"
                        Expect.equal (File.ReadAllText(tempFile)) "Hello Expecto!" "File content match"
                    | outcome -> failtestf "Expected completed outcome, got %A" outcome
                }
            }
        ]
```

---

## 🎯 Acceptance Criteria

1. **Sandbox Enforcement**: `validateSandboxPath` blocks 100% of path traversal attempts (`../..`, `C:\Windows`).
2. **Process Timeout Protection**: Commands timing out after 60s are terminated cleanly without hanging the turn.
3. **Output Cap**: Tool outputs exceeding 100 KB are truncated automatically with a diagnostic marker.
4. **MCP stdio Parity**: `McpClient` connects to stdio MCP processes, discovers `tools/list`, and routes `tools/call`.
5. **Test Suite 100% Pass**: `dotnet test implementations/fsharp/Skight.AgentPlatform.FSharp.sln` passes 100%.
