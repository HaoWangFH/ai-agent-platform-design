namespace Skight.AgentPlatform.FSharp

open System.Collections.Generic
open System.Text.Json

type ToolRegistry() =
    let tools = Dictionary<string, string -> Async<string>>()
    let schemas = List<ToolSchema>()

    member this.Register(name: string, description: string, handler: string -> Async<string>, parametersJson: string) =
        tools.[name] <- handler

        use _ = JsonDocument.Parse(parametersJson)
        let schema = {
            Name = name
            Description = description
            ParametersJson = parametersJson
        }
        schemas.Add(schema)

    member this.ExecuteToolAsync(name: string, argumentsJson: string) : Async<string> =
        async {
            match tools.TryGetValue(name) with
            | true, handler ->
                try
                    return! handler argumentsJson
                with ex ->
                    return sprintf "Error executing tool '%s': %s" name ex.Message
            | false, _ ->
                return sprintf "Error: Tool '%s' not found." name
        }

    member this.AsExecutor : ToolExecutor =
        fun name args -> this.ExecuteToolAsync(name, args)

    member this.GetToolSchemas() : ToolSchema list =
        Seq.toList schemas

    member this.GetRegisteredNames() : string list =
        Seq.toList tools.Keys
