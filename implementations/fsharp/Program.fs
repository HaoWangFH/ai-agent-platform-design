namespace AgentPlatform.FSharp

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

    [<EntryPoint>]
    let main argv =
        loadDotEnv ()

        let apiKey = 
            match Environment.GetEnvironmentVariable("AZURE_API_KEY") with
            | null | "" -> Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            | k -> k

        let isMockMode = String.IsNullOrEmpty apiKey
        let effectiveKey = if isMockMode then "dummy_key" else apiKey

        if isMockMode then
            printfn "⚠️ Warning: No OPENAI_API_KEY or AZURE_API_KEY found in environment or .env file."
            printfn "   To make live LLM API calls, set OPENAI_API_KEY in your environment or create a .env file."
            printfn "   Running unit tests via 'dotnet test' uses simulated mock LLMs and does not require an API key.\n"

        printfn "Initializing F# Agent..."

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
            Model = "gpt-4o"
        }

        let agent = Agent(effectiveKey, registry, config)

        printfn "Agent is ready. Type 'exit' or 'quit' to stop."

        let mutable running = true
        while running do
            printf "> "
            let input = Console.ReadLine()
            if isNull input then
                running <- false
            else
                let trimmed = input.Trim()
                if trimmed.ToLower() = "exit" || trimmed.ToLower() = "quit" then
                    running <- false
                elif not (String.IsNullOrEmpty trimmed) then
                    try
                        let result = agent.RunAsync(trimmed) |> Async.RunSynchronously
                        if result.Failed then
                            match result.Error with
                            | Some err when isMockMode ->
                                printfn "❌ API Call Failed: %s" err
                                printfn "💡 Hint: Please set OPENAI_API_KEY environment variable or create a .env file with OPENAI_API_KEY=your_key to connect to live OpenAI/Azure endpoints."
                            | Some err ->
                                printfn "❌ API Call Failed: %s" err
                            | None -> ()
                    with ex ->
                        printfn "Error: %s" ex.Message

        0
