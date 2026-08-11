namespace Skight.AgentPlatform.FSharp

open System
open System.IO
open System.Text.Json

module Program =

    let loadDotEnv () =
        let rec findEnv path =
            if File.Exists(path) then Some path
            else
                let parent = Directory.GetParent(Path.GetDirectoryName(Path.GetFullPath(path)))
                if isNull parent then None
                else findEnv (Path.Combine(parent.FullName, ".env"))

        match findEnv ".env" with
        | Some envPath ->
            printfn "Loaded environment from %s" envPath
            File.ReadAllLines(envPath)
            |> Array.iter (fun line ->
                if not (String.IsNullOrWhiteSpace(line)) && not (line.StartsWith("#")) then
                    let parts = line.Split('=', 2)
                    if parts.Length = 2 then
                        Environment.SetEnvironmentVariable(parts.[0].Trim(), parts.[1].Trim().Trim('"'))
            )
        | None -> ()

    let getEntraIdTokenAsync (tenantId: string) (clientId: string) (clientSecret: string) (audienceId: string) : Async<string option> =
        async {
            try
                use client = new System.Net.Http.HttpClient()
                let pairs = [
                    System.Collections.Generic.KeyValuePair<string, string>("grant_type", "client_credentials")
                    System.Collections.Generic.KeyValuePair<string, string>("client_id", clientId)
                    System.Collections.Generic.KeyValuePair<string, string>("client_secret", clientSecret)
                    System.Collections.Generic.KeyValuePair<string, string>("scope", sprintf "%s/.default" audienceId)
                ]
                use content = new System.Net.Http.FormUrlEncodedContent(pairs)
                let! response = client.PostAsync(sprintf "https://login.microsoftonline.com/%s/oauth2/v2.0/token" tenantId, content) |> Async.AwaitTask
                if not response.IsSuccessStatusCode then
                    printfn "Failed to fetch Entra ID token: %O" response.StatusCode
                    return None
                else
                    let! json = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    use doc = JsonDocument.Parse(json)
                    return Some (doc.RootElement.GetProperty("access_token").GetString())
            with ex ->
                printfn "Error fetching Entra token: %s" ex.Message
                return None
        }

    [<EntryPoint>]
    let main argv =
        loadDotEnv ()

        let apiKey = 
            match Environment.GetEnvironmentVariable("AZURE_API_KEY") with
            | null | "" -> Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            | k -> k

        let gatewayEndpoint = Environment.GetEnvironmentVariable("GATEWAY_ENDPOINT")
        let tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID")
        let clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID")
        let clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET")
        let audienceId = Environment.GetEnvironmentVariable("GATEWAY_AUDIENCE_ID")

        let mutable endpoint : string option = None
        let mutable selectedModel = "gpt-4o"
        let mutable jwtToken : string option = None

        if not (String.IsNullOrEmpty gatewayEndpoint) then
            let uri = Uri(gatewayEndpoint)
            endpoint <- Some (sprintf "%s://%s/" uri.Scheme uri.Host)
            if gatewayEndpoint.Contains("/deployments/") then
                let parts = gatewayEndpoint.Split("/deployments/")
                if parts.Length > 1 then
                    selectedModel <- parts.[1].Split('/').[0]

        if not (String.IsNullOrEmpty tenantId) && not (String.IsNullOrEmpty clientId) && not (String.IsNullOrEmpty clientSecret) && not (String.IsNullOrEmpty audienceId) then
            printfn "Fetching Entra ID token..."
            jwtToken <- getEntraIdTokenAsync tenantId clientId clientSecret audienceId |> Async.RunSynchronously

        let isMockMode = String.IsNullOrEmpty apiKey
        let effectiveKey = if isMockMode then "dummy_key" else apiKey

        if isMockMode then
            printfn "⚠️ Warning: No OPENAI_API_KEY or AZURE_API_KEY found in environment or .env file."
            printfn "   To make live LLM API calls, set OPENAI_API_KEY in your environment or create a .env file."
            printfn "   Running unit tests via 'dotnet test' uses simulated mock LLMs and does not require an API key.\n"

        printfn "Initializing F# Agent (Model: %s)..." selectedModel

        let registry = ToolRegistry()
        let workspaceRoot =
            match Environment.GetEnvironmentVariable("AGENT_WORKSPACE_ROOT") with
            | null | "" -> Directory.GetCurrentDirectory()
            | value -> value

        let approvalPrompt = ApprovalGuard.createConsolePrompt()

        // Register mock example tool
        registry.Register(
            "get_weather",
            "Get the current weather for a location",
            (fun argsJson ->
                async {
                    try
                        use doc = JsonDocument.Parse(argsJson)
                        let root = doc.RootElement
                        let loc = if root.TryGetProperty("location") <> (false, Unchecked.defaultof<_>) then root.GetProperty("location").GetString() else "unknown"
                        let unit = if root.TryGetProperty("unit") <> (false, Unchecked.defaultof<_>) then root.GetProperty("unit").GetString() else "celsius"
                        if loc.ToLower().Contains("san francisco") then
                            return sprintf "The weather in %s is 16 degrees %s and foggy." loc unit
                        else
                            return sprintf "The weather in %s is 22 degrees %s and sunny." loc unit
                    with _ ->
                        return "Weather data unavailable."
                }),
            """{"type":"object","properties":{"location":{"type":"string"},"unit":{"type":"string"}},"required":["location"]}"""
        )

        registry.Register(
            "read_file",
            "Read file contents from disk inside sandbox workspace",
            (fun argsJson -> FileTools.readFileTool workspaceRoot argsJson),
            """{"type":"object","properties":{"path":{"type":"string"},"max_bytes":{"type":"integer"}},"required":["path"]}"""
        )

        registry.Register(
            "write_file",
            "Write file contents to disk inside sandbox workspace",
            (fun argsJson ->
                async {
                    match! ApprovalGuard.requireFileEditApproval approvalPrompt "write_file" argsJson with
                    | Error err -> return sprintf "Approval denied: %s" err
                    | Ok () -> return! FileTools.writeFileTool workspaceRoot argsJson
                }),
            """{"type":"object","properties":{"path":{"type":"string"},"content":{"type":"string"}},"required":["path","content"]}"""
        )

        registry.Register(
            "edit_file",
            "Edit file contents via search/replace or SEARCH/REPLACE diff blocks inside sandbox workspace",
            (fun argsJson ->
                async {
                    match! ApprovalGuard.requireFileEditApproval approvalPrompt "edit_file" argsJson with
                    | Error err -> return sprintf "Approval denied: %s" err
                    | Ok () -> return! FileTools.editFileTool workspaceRoot argsJson
                }),
            """{"type":"object","properties":{"path":{"type":"string"},"search":{"type":"string"},"replace":{"type":"string"},"patch":{"type":"string"}},"required":["path"]}"""
        )

        registry.Register(
            "execute_command",
            TerminalTool.getToolDescription (),
            (fun argsJson ->
                async {
                    try
                        use doc = JsonDocument.Parse(argsJson)
                        let root = doc.RootElement
                        let command = root.GetProperty("command").GetString()

                        match! ApprovalGuard.requireCommandApproval approvalPrompt command with
                        | Error err ->
                            return sprintf "Approval denied: %s" err
                        | Ok () ->
                            let timeoutMs =
                                let mutable timeoutElement = Unchecked.defaultof<JsonElement>
                                if root.TryGetProperty("timeout_ms", &timeoutElement) then timeoutElement.GetInt32() else 60_000

                            let maxOutputBytes =
                                let mutable maxElement = Unchecked.defaultof<JsonElement>
                                if root.TryGetProperty("max_output_bytes", &maxElement) then maxElement.GetInt32() else 100 * 1024

                            let background =
                                let mutable bgElement = Unchecked.defaultof<JsonElement>
                                if root.TryGetProperty("background", &bgElement) then bgElement.GetBoolean() else false

                            if background then
                                match TerminalTool.startBackgroundCommand command with
                                | Ok handle -> return sprintf "Started background command %s at %O" handle.Id handle.StartedAtUtc
                                | Error err -> return sprintf "Error: %s" err
                            else
                                return! TerminalTool.executeCommandAsync timeoutMs maxOutputBytes command
                    with ex ->
                        return sprintf "Error executing command tool: %s" ex.Message
                }),
            """{"type":"object","properties":{"command":{"type":"string"},"timeout_ms":{"type":"integer"},"max_output_bytes":{"type":"integer"},"background":{"type":"boolean"}},"required":["command"]}"""
        )

        registry.Register("git_status", "Get the git status of the workspace.", (fun _ -> GitTools.gitStatus workspaceRoot |> Async.AwaitTask), """{"type":"object","properties":{}}""")
        registry.Register("git_commit", "Stage and commit changes in workspace.", (fun argsJson -> GitTools.gitCommit workspaceRoot argsJson |> Async.AwaitTask), """{"type":"object","properties":{"message":{"type":"string"}},"required":["message"]}""")
        registry.Register("git_push", "Push committed changes to origin.", (fun _ -> GitTools.gitPush workspaceRoot |> Async.AwaitTask), """{"type":"object","properties":{}}""")
        registry.Register("web_fetch_content", "Fetch text content from a web URL.", (fun argsJson -> WebTools.fetchUrlContentAsync argsJson |> Async.AwaitTask), """{"type":"object","properties":{"url":{"type":"string"}},"required":["url"]}""")
        registry.Register("store_memory", "Store a key-value memory.", (fun argsJson -> MemoryTool.storeMemoryAsync argsJson |> Async.AwaitTask), """{"type":"object","properties":{"key":{"type":"string"},"value":{"type":"string"}},"required":["key","value"]}""")
        registry.Register("recall_memory", "Recall a stored memory by key.", (fun argsJson -> MemoryTool.recallMemoryAsync argsJson |> Async.AwaitTask), """{"type":"object","properties":{"key":{"type":"string"}},"required":["key"]}""")
        registry.Register("add_todo", "Add a task to the session TODO list.", (fun argsJson -> TodoTool.addTodoAsync argsJson |> Async.AwaitTask), """{"type":"object","properties":{"task":{"type":"string"}},"required":["task"]}""")
        registry.Register("list_todos", "List all session TODO tasks.", (fun _ -> TodoTool.listTodosAsync () |> Async.AwaitTask), """{"type":"object","properties":{}}""")
        registry.Register("complete_todo", "Mark a session TODO task as complete.", (fun argsJson -> TodoTool.completeTodoAsync argsJson |> Async.AwaitTask), """{"type":"object","properties":{"id":{"type":"integer"}},"required":["id"]}""")
        registry.Register("git_diff", "Get uncommitted git diff of workspace.", (fun _ -> GitTools.gitDiff workspaceRoot |> Async.AwaitTask), """{"type":"object","properties":{}}""")
        registry.Register("git_log", "Get recent git commit history.", (fun argsJson -> GitTools.gitLog workspaceRoot argsJson |> Async.AwaitTask), """{"type":"object","properties":{"count":{"type":"integer"}}}""")
        registry.Register("system_info", "Get environment and system information.", (fun _ -> SystemInfoTool.getSystemInfoAsync workspaceRoot |> Async.AwaitTask), """{"type":"object","properties":{}}""")
        registry.Register("inspect_image", "Inspect image file metadata and encode as Base64 Data URI.", (fun argsJson -> MediaTools.inspectImageAsync workspaceRoot argsJson |> Async.AwaitTask), """{"type":"object","properties":{"path":{"type":"string"}},"required":["path"]}""")
        registry.Register("inspect_audio", "Inspect audio file metadata and encode as Base64 Data URI.", (fun argsJson -> MediaTools.inspectAudioAsync workspaceRoot argsJson |> Async.AwaitTask), """{"type":"object","properties":{"path":{"type":"string"}},"required":["path"]}""")
        registry.Register("transcribe_audio", "Transcribe audio speech into text.", (fun argsJson -> MediaTools.transcribeAudioAsync workspaceRoot argsJson |> Async.AwaitTask), """{"type":"object","properties":{"path":{"type":"string"}},"required":["path"]}""")
        registry.Register("text_to_speech", "Synthesize text into spoken audio file.", (fun argsJson -> MediaTools.textToSpeechAsync workspaceRoot argsJson |> Async.AwaitTask), """{"type":"object","properties":{"text":{"type":"string"},"output_path":{"type":"string"}},"required":["text"]}""")
        registry.Register("send_webhook", "Send HTTP REST Webhook payload to external integration endpoints.", (fun argsJson -> IntegrationTools.sendWebhookAsync argsJson |> Async.AwaitTask), """{"type":"object","properties":{"url":{"type":"string"},"method":{"type":"string"},"payload":{"type":"object"}},"required":["url"]}""")
        registry.Register("schedule_timer", "Schedule a background automation timer notification.", (fun argsJson -> AutomationTools.scheduleTimerAsync argsJson |> Async.AwaitTask), """{"type":"object","properties":{"seconds":{"type":"integer"},"prompt":{"type":"string"}},"required":["seconds","prompt"]}""")

        // Optional MCP Server Auto-Discovery via environment variables
        let mcpCmd = Environment.GetEnvironmentVariable("MCP_SERVER_CMD")
        let mcpArgs = Environment.GetEnvironmentVariable("MCP_SERVER_ARGS")

        let mutable mcpClientOpt : McpClient option = None

        if not (String.IsNullOrWhiteSpace(mcpCmd)) then
            let argsStr = if isNull mcpArgs then "" else mcpArgs
            printfn "Connecting to external MCP Server: %s %s..." mcpCmd argsStr
            let client = new McpClient(mcpCmd, argsStr)
            mcpClientOpt <- Some client
            match registry.RegisterMcpClientAsync(client) |> Async.RunSynchronously with
            | Ok count -> printfn "  [MCP Auto-Discovery] Successfully registered %d MCP tool(s)!" count
            | Error err -> printfn "  [MCP Auto-Discovery Error] Failed to register MCP server: %s" err

        let config = {
            MaxIterations = 10
            MaxRetries = 3
            ContextWindowLimit = 30
            Model = selectedModel
        }

        let agent = Agent(effectiveKey, registry, config, ?endpoint=endpoint, ?jwtToken=jwtToken)
        printfn "Agent is ready. Type 'exit' or 'quit' to stop."

        let executeTurn prompt session =
            try
                let isLiveStreaming = not isMockMode

                let result, nextSession =
                    if isLiveStreaming then
                        let mutable hasPrintedText = false
                        let onChunk chunk =
                            match chunk with
                            | TextDelta text when not (String.IsNullOrEmpty(text)) ->
                                if not hasPrintedText then
                                    printf "Assistant: "
                                    hasPrintedText <- true
                                printf "%s" text
                            | StreamCompleted _ when hasPrintedText ->
                                printfn ""
                            | _ -> ()

                        agent.RunPureStreamingAsync(prompt, session, onChunk)
                        |> Async.RunSynchronously
                    else
                        let res, nextSess =
                            agent.RunPureAsync(prompt, session)
                            |> Async.RunSynchronously
                        match res.Outcome with
                        | TurnOutcome.Completed text -> printfn "Assistant: %s" text
                        | _ -> ()
                        res, nextSess

                match result.Outcome with
                | TurnOutcome.Failed reason when isMockMode ->
                    let err = match reason with | FailureReason.ApiError e -> e | FailureReason.BudgetExhausted e -> e | FailureReason.NoResponse e -> e
                    printfn "❌ API Call Failed: %s" err
                    printfn "💡 Hint: Please set OPENAI_API_KEY environment variable or create a .env file with OPENAI_API_KEY=your_key to connect to live OpenAI/Azure endpoints."
                | TurnOutcome.Failed reason ->
                    let err = match reason with | FailureReason.ApiError e -> e | FailureReason.BudgetExhausted e -> e | FailureReason.NoResponse e -> e
                    printfn "❌ API Call Failed: %s" err
                | TurnOutcome.Completed _
                | TurnOutcome.Interrupted -> ()

                nextSession
            with ex ->
                printfn "Error: %s" ex.Message
                session

        let rec loop session =
            printf "> "
            let input = Console.ReadLine()
            if isNull input then 0
            else
                let trimmed = input.Trim()
                if trimmed.ToLower() = "exit" || trimmed.ToLower() = "quit" then 0
                elif String.IsNullOrEmpty trimmed then loop session
                else
                    let nextSess = executeTurn trimmed session
                    loop nextSess

        let initialSession = agent.CreateInitialSession()
        try
            if argv.Length > 0 then
                let promptFromArgs = String.Join(" ", argv)
                printfn "Processing prompt: %s" promptFromArgs
                let _ = executeTurn promptFromArgs initialSession
                AgentTelemetry.flush ()
                0
            else
                loop initialSession
        finally
            match mcpClientOpt with
            | Some client -> client.Dispose()
            | None -> ()
