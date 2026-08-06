namespace Skight.AgentPlatform.FSharp

open System
open System.Diagnostics
open System.IO
open System.Threading
open System.Threading.Tasks

type McpClient(command: string, args: string) =
    let proc = new Process()
    let mutable nextReqId = 1L
    let mutable isDisposed = false

    do
        proc.StartInfo.FileName <- command
        proc.StartInfo.Arguments <- args
        proc.StartInfo.RedirectStandardInput <- true
        proc.StartInfo.RedirectStandardOutput <- true
        proc.StartInfo.RedirectStandardError <- true
        proc.StartInfo.UseShellExecute <- false
        proc.StartInfo.CreateNoWindow <- true
        
        try
            if not (proc.Start()) then
                invalidOp (sprintf "Failed to start MCP server process '%s %s'" command args)
        with ex ->
            invalidOp (sprintf "Failed to launch MCP server process '%s %s': %s" command args ex.Message)

    member private _.GetNextId() =
        Interlocked.Increment(&nextReqId)

    member _.SendRequestAsync(reqJson: string, ?timeoutMs: int) : Async<string> =
        async {
            let timeout = defaultArg timeoutMs 30_000
            if proc.HasExited then
                return sprintf """{"jsonrpc":"2.0","id":0,"error":{"code":-32000,"message":"MCP server process exited with code %d"}}""" proc.ExitCode
            else
                try
                    do! proc.StandardInput.WriteLineAsync(reqJson) |> Async.AwaitTask
                    do! proc.StandardInput.FlushAsync() |> Async.AwaitTask

                    let readTask = proc.StandardOutput.ReadLineAsync()
                    let! completed = Task.WhenAny(readTask, Task.Delay(timeout)) |> Async.AwaitTask

                    if obj.ReferenceEquals(completed, readTask) then
                        let! respLine = readTask |> Async.AwaitTask
                        if isNull respLine then
                            return """{"jsonrpc":"2.0","id":0,"error":{"code":-32000,"message":"MCP server closed stdout stream"}}"""
                        else
                            return respLine
                    else
                        return sprintf """{"jsonrpc":"2.0","id":0,"error":{"code":-32000,"message":"MCP server request timed out after %d ms"}}""" timeout
                with ex ->
                    return sprintf """{"jsonrpc":"2.0","id":0,"error":{"code":-32000,"message":"IPC communication error: %s"}}""" ex.Message
        }

    member this.InitializeAsync(?clientName: string, ?clientVersion: string) : Async<Result<string, string>> =
        async {
            let cName = defaultArg clientName "Skight.AgentPlatform.FSharp"
            let cVer = defaultArg clientVersion "1.0.0"
            let id = this.GetNextId()
            let reqJson = McpProtocol.createInitializeRequest id cName cVer
            let! respJson = this.SendRequestAsync(reqJson)
            
            if respJson.Contains("\"error\"") && not (respJson.Contains("\"result\"")) then
                return Error (sprintf "MCP Initialization failed: %s" respJson)
            else
                return Ok respJson
        }

    member this.ListToolsAsync() : Async<Result<ToolSchema list, string>> =
        async {
            let id = this.GetNextId()
            let reqJson = McpProtocol.createToolsListRequest id
            let! respJson = this.SendRequestAsync(reqJson)
            return McpSchemaTranslator.parseToolsListResponse respJson
        }

    member this.CallToolAsync(toolName: ToolName, argumentsJson: string) : Async<string> =
        async {
            let id = this.GetNextId()
            let nameStr = ToolName.value toolName
            let reqJson = McpProtocol.createToolsCallRequest id nameStr argumentsJson
            let! respJson = this.SendRequestAsync(reqJson)
            return McpSchemaTranslator.parseToolCallResponse respJson
        }

    member _.Dispose() =
        if not isDisposed then
            isDisposed <- true
            try
                if not proc.HasExited then
                    proc.Kill(true)
            with _ -> ()
            proc.Dispose()

    interface IDisposable with
        member this.Dispose() = this.Dispose()
