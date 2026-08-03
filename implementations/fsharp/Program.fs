namespace AgentPlatform.FSharp

open System
open System.IO
open System.Text.Json

module Program =

    [<EntryPoint>]
    let main argv =
        let apiKey = 
            match Environment.GetEnvironmentVariable("AZURE_API_KEY") with
            | null | "" -> Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            | k -> k

        let effectiveKey = if String.IsNullOrEmpty apiKey then "dummy_key" else apiKey

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
                        ignore result
                    with ex ->
                        printfn "Error: %s" ex.Message

        0
