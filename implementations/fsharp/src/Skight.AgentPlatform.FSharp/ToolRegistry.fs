namespace Skight.AgentPlatform.FSharp

open System.Collections.Generic
open System.Text.Json

type ToolRegistry() =
    let tools = Dictionary<ToolName, string -> Async<string>>()
    let schemas = List<ToolSchema>()

    member this.Register(nameStr: string, description: string, handler: string -> Async<string>, parametersJson: string) =
        match ToolName.create nameStr with
        | Ok name ->
            tools.[name] <- handler

            use _ = JsonDocument.Parse(parametersJson)
            let schema = {
                Name = name
                Description = description
                ParametersJson = parametersJson
            }
            schemas.Add(schema)
        | Error err -> failwithf "Invalid tool name '%s': %s" nameStr err

    member this.ExecuteToolAsync(name: ToolName, argumentsJson: string) : Async<string> =
        async {
            match tools.TryGetValue(name) with
            | true, handler ->
                try
                    return! handler argumentsJson
                with ex ->
                    return sprintf "Error executing tool '%s': %s" (ToolName.value name) ex.Message
            | false, _ ->
                return sprintf "Error: Tool '%s' not found." (ToolName.value name)
        }

    member this.AsExecutor : ToolExecutor =
        fun name args -> this.ExecuteToolAsync(name, args)

    member this.GetToolSchemas() : ToolSchema list =
        Seq.toList schemas

    member this.GetRegisteredNames() : ToolName list =
        Seq.toList tools.Keys
