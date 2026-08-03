namespace Skight.AgentPlatform.FSharp

open System
open System.Collections.Generic
open System.Text.Json
open Azure.AI.OpenAI

type ToolRegistry() =
    let tools = Dictionary<string, string -> Async<string>>()
    let schemas = List<FunctionDefinition>()

    member this.Register(name: string, description: string, handler: string -> Async<string>, parametersJson: string) =
        tools.[name] <- handler
        
        use doc = JsonDocument.Parse(parametersJson)
        let functionDef = FunctionDefinition(
            Name = name,
            Description = description,
            Parameters = BinaryData.FromString(parametersJson)
        )
        schemas.Add(functionDef)

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

    member this.GetToolSchemas() : FunctionDefinition list =
        Seq.toList schemas

    member this.GetRegisteredNames() : string list =
        Seq.toList tools.Keys
