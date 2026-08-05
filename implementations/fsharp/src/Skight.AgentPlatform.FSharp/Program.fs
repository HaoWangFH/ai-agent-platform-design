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

        // Register mock tools
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
            "Read file contents from disk",
            (fun argsJson ->
                async {
                    try
                        use doc = JsonDocument.Parse(argsJson)
                        let path = doc.RootElement.GetProperty("path").GetString()
                        if File.Exists(path) then
                            let content = File.ReadAllText(path)
                            return content
                        else
                            return sprintf "Error: File '%s' not found." path
                    with ex ->
                        return sprintf "Error reading file: %s" ex.Message
                }),
            """{"type":"object","properties":{"path":{"type":"string"}},"required":["path"]}"""
        )

        let config = {
            MaxIterations = 10
            MaxRetries = 3
            ContextWindowLimit = 30
            Model = selectedModel
        }

        let agent = Agent(effectiveKey, registry, config, ?endpoint=endpoint, ?jwtToken=jwtToken)
        printfn "Agent is ready. Type 'exit' or 'quit' to stop."

        let rec loop session =
            printf "> "
            let input = Console.ReadLine()
            if isNull input then 0
            else
                let trimmed = input.Trim()
                if trimmed.ToLower() = "exit" || trimmed.ToLower() = "quit" then 0
                elif String.IsNullOrEmpty trimmed then loop session
                else
                    try
                        let isLiveStreaming = not isMockMode

                        let result, nextSession =
                            if isLiveStreaming then
                                let mutable hasPrintedText = false
                                let onChunk chunk =
                                    match chunk with
                                    | TextDelta text when not (String.IsNullOrEmpty(text)) ->
                                        if not hasPrintedText then
                                            printf "Assistant (stream): "
                                            hasPrintedText <- true
                                        printf "%s" text
                                    | StreamCompleted _ when hasPrintedText ->
                                        printfn ""
                                    | _ -> ()

                                agent.RunPureStreamingAsync(trimmed, session, onChunk)
                                |> Async.RunSynchronously
                            else
                                agent.RunPureAsync(trimmed, session)
                                |> Async.RunSynchronously

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

                        loop nextSession
                    with ex ->
                        printfn "Error: %s" ex.Message
                        loop session

        let initialSession = agent.CreateInitialSession()
        loop initialSession
