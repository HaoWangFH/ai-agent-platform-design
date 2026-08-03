namespace AgentPlatform.FSharp

open System
open System.Collections.Concurrent
open Azure.AI.OpenAI

type ToolRegistry() =
    let tools = ConcurrentDictionary<string, ToolDefinition>()

    member _.Register(name: string, description: string, handler: string -> Async<string>, parametersJson: string) =
        let def = { Name = name; Description = description; ParametersJson = parametersJson; Handler = handler }
        tools.[name] <- def

    member _.GetToolSchemas() : FunctionDefinition list =
        tools.Values
        |> Seq.map (fun t ->
            let fn = FunctionDefinition(Name = t.Name, Description = t.Description)
            if not (String.IsNullOrEmpty(t.ParametersJson)) then
                fn.Parameters <- BinaryData.FromString(t.ParametersJson)
            fn)
        |> Seq.toList

    member _.GetRegisteredNames() : string list =
        tools.Keys |> Seq.toList

    member _.ExecuteToolAsync(name: string, argsJson: string) : Async<string> =
        async {
            match tools.TryGetValue(name) with
            | true, toolDef ->
                try
                    return! toolDef.Handler argsJson
                with ex ->
                    return sprintf "Error executing tool '%s': %s" name ex.Message
            | false, _ ->
                let avail = tools.Keys |> String.concat ", "
                return sprintf "Error: Tool '%s' is not registered. Available tools: %s" name avail
        }

    /// Exposes tool execution as a composable, partially applicable function (ToolExecutor)
    member self.AsExecutor : ToolExecutor =
        fun name argsJson -> self.ExecuteToolAsync(name, argsJson)
