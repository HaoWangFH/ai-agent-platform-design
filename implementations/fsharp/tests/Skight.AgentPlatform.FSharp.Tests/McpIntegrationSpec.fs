namespace Skight.AgentPlatform.FSharp.Tests

open Expecto
open System
open System.IO
open Skight.AgentPlatform.FSharp

module McpIntegrationSpec =

    let testConfig = {
        Model = "gpt-4o"
        MaxIterations = 5
        ContextWindowLimit = 20
        MaxRetries = 2
    }

    let workspaceRoot = Directory.GetCurrentDirectory()

    [<Tests>]
    let mcpSpecTests =
        testList "Feature 4: MCP (Model Context Protocol) Integration Spec Suite" [

            testAsync "4.1 JSON-RPC 2.0 Protocol Serializer" {
                let initReq = McpProtocol.createInitializeRequest 100L "TestClient" "1.0"
                Expect.stringContains initReq "\"jsonrpc\":\"2.0\"" "Should contain jsonrpc 2.0"
                Expect.stringContains initReq "\"id\":100" "Should contain request id 100"
                Expect.stringContains initReq "\"method\":\"initialize\"" "Should contain method initialize"

                let toolsListReq = McpProtocol.createToolsListRequest 101L
                Expect.stringContains toolsListReq "\"method\":\"tools/list\"" "Should contain method tools/list"

                let toolsCallReq = McpProtocol.createToolsCallRequest 102L "calculator" """{"a":5,"b":10}"""
                Expect.stringContains toolsCallReq "\"name\":\"calculator\"" "Should contain tool name calculator"
            }

            testAsync "4.2 MCP Schema Translator (tools/list & tools/call)" {
                let sampleToolsListJson = """
                {
                    "jsonrpc": "2.0",
                    "id": 1,
                    "result": {
                        "tools": [
                            {
                                "name": "sqlite_query",
                                "description": "Execute raw SQL query on SQLite database",
                                "inputSchema": {
                                    "type": "object",
                                    "properties": {
                                        "query": { "type": "string" }
                                    },
                                    "required": ["query"]
                                }
                            }
                        ]
                    }
                }
                """
                match McpSchemaTranslator.parseToolsListResponse sampleToolsListJson with
                | Error err -> Tests.failtest (sprintf "Translation failed: %s" err)
                | Ok schemas ->
                    Expect.equal schemas.Length 1 "Should translate 1 tool schema"
                    let schema = schemas.[0]
                    Expect.equal (ToolName.value schema.Name) "sqlite_query" "Tool name should match"
                    Expect.stringContains schema.Description "SQLite database" "Description should match"
                    Expect.stringContains schema.ParametersJson "query" "Parameters JSON should contain query field"

                let sampleToolCallJson = """
                {
                    "jsonrpc": "2.0",
                    "id": 2,
                    "result": {
                        "content": [
                            {
                                "type": "text",
                                "text": "Query result: 42 rows returned."
                            }
                        ]
                    }
                }
                """
                let callResult = McpSchemaTranslator.parseToolCallResponse sampleToolCallJson
                Expect.equal callResult "Query result: 42 rows returned." "Tool call result should match"
            }

            testAsync "4.3 Mock Subprocess IPC & McpClient Lifecycle" {
                let isWindows = Environment.OSVersion.Platform = PlatformID.Win32NT
                let shell = if isWindows then "powershell.exe" else "/bin/bash"

                let psScript = """
                $stdin = [Console]::In
                while (($line = $stdin.ReadLine()) -ne $null) {
                    if ($line -like '*"method":"initialize"*') {
                        Write-Output '{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2024-11-05","capabilities":{},"serverInfo":{"name":"MockServer","version":"1.0"}}}'
                    }
                    elseif ($line -like '*"method":"tools/list"*') {
                        Write-Output '{"jsonrpc":"2.0","id":2,"result":{"tools":[{"name":"mock_mcp_tool","description":"Mock tool for spec test","inputSchema":{"type":"object","properties":{"input":{"type":"string"}}}}]}}'
                    }
                    elseif ($line -like '*"method":"tools/call"*') {
                        Write-Output '{"jsonrpc":"2.0","id":3,"result":{"content":[{"type":"text","text":"Mock MCP execution success"}]}}'
                    }
                }
                """

                let scriptPath = Path.Combine(Path.GetTempPath(), sprintf "mock_mcp_server_%s.ps1" (Guid.NewGuid().ToString("N")))
                File.WriteAllText(scriptPath, psScript)

                try
                    let args = sprintf "-NoProfile -ExecutionPolicy Bypass -File \"%s\"" scriptPath
                    use mcpClient = new McpClient(shell, args)

                    match! mcpClient.InitializeAsync() with
                    | Error err -> Tests.failtest (sprintf "Initialization failed: %s" err)
                    | Ok _ -> ()

                    match! mcpClient.ListToolsAsync() with
                    | Error err -> Tests.failtest (sprintf "ListTools failed: %s" err)
                    | Ok schemas ->
                        Expect.equal schemas.Length 1 "Should discover 1 mock MCP tool"
                        Expect.equal (ToolName.value schemas.[0].Name) "mock_mcp_tool" "Tool name should match"

                        match ToolName.create "mock_mcp_tool" with
                        | Error _ -> Tests.failtest "ToolName creation failed"
                        | Ok toolName ->
                            let! resultText = mcpClient.CallToolAsync(toolName, """{"input":"test"}""")
                            Expect.equal resultText "Mock MCP execution success" "Execution output should match"
                finally
                    if File.Exists scriptPath then
                        try File.Delete scriptPath with _ -> ()
            }

            testAsync "4.4 Registry Auto-Discovery & Pipeline Integration with MCP Tool" {
                let isWindows = Environment.OSVersion.Platform = PlatformID.Win32NT
                let shell = if isWindows then "powershell.exe" else "/bin/bash"

                let psScript = """
                $stdin = [Console]::In
                while (($line = $stdin.ReadLine()) -ne $null) {
                    if ($line -like '*"method":"initialize"*') {
                        Write-Output '{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2024-11-05","capabilities":{},"serverInfo":{"name":"MockServer","version":"1.0"}}}'
                    }
                    elseif ($line -like '*"method":"tools/list"*') {
                        Write-Output '{"jsonrpc":"2.0","id":2,"result":{"tools":[{"name":"ping_mcp","description":"Ping MCP tool","inputSchema":{"type":"object","properties":{}}}]}}'
                    }
                    elseif ($line -like '*"method":"tools/call"*') {
                        Write-Output '{"jsonrpc":"2.0","id":3,"result":{"content":[{"type":"text","text":"pong"}]}}'
                    }
                }
                """

                let scriptPath = Path.Combine(Path.GetTempPath(), sprintf "mock_mcp_registry_%s.ps1" (Guid.NewGuid().ToString("N")))
                File.WriteAllText(scriptPath, psScript)

                try
                    let args = sprintf "-NoProfile -ExecutionPolicy Bypass -File \"%s\"" scriptPath
                    use mcpClient = new McpClient(shell, args)
                    let registry = ToolRegistry()

                    match! registry.RegisterMcpClientAsync(mcpClient, "remote") with
                    | Error err -> Tests.failtest (sprintf "Registry auto-discovery failed: %s" err)
                    | Ok count ->
                        Expect.equal count 1 "Should register 1 tool"
                        let schemas = registry.GetToolSchemas()
                        Expect.equal schemas.Length 1 "Registry schemas count should be 1"
                        Expect.equal (ToolName.value schemas.[0].Name) "remote_ping_mcp" "Tool name should have prefix"

                        let callId = ToolCallId.create "call_mcp_1" |> Result.defaultWith (fun _ -> failwith "invalid id")
                        let toolName = ToolName.create "remote_ping_mcp" |> Result.defaultWith (fun _ -> failwith "invalid name")

                        // Pipeline Execution Mock
                        let mockLlmResponses = [
                            // Turn 1: Call MCP tool
                            Ok {
                                Content = ""
                                ToolCalls = [
                                    { Id = callId
                                      Name = toolName
                                      ArgumentsJson = "{}" }
                                ]
                            }
                            // Turn 2: Final response
                            Ok {
                                Content = "Received pong from remote MCP server."
                                ToolCalls = []
                            }
                        ]

                        let mutable callIndex = 0
                        let mockLlmCaller : LlmCaller =
                            fun _ _ ->
                                async {
                                    let resp = mockLlmResponses.[callIndex]
                                    callIndex <- callIndex + 1
                                    return resp
                                }

                        let session = AgentSession.initialize "Test System Prompt"
                        let registeredNamesSet = registry.GetRegisteredNames() |> Set.ofList

                        let! turnResult, _ = AgentRunner.runTurnAsync mockLlmCaller registry.AsExecutor testConfig "Ping MCP" session schemas registeredNamesSet

                        match turnResult.Outcome with
                        | TurnOutcome.Completed text ->
                            Expect.equal text "Received pong from remote MCP server." "Final pipeline response should match"
                        | other ->
                            Tests.failtest (sprintf "Pipeline failed: %A" other)
                finally
                    if File.Exists scriptPath then
                        try File.Delete scriptPath with _ -> ()
            }
        ]
