namespace Skight.AgentPlatform.FSharp

open System
open System.Text.Json

module ClarifyTool =

    let schema : ToolSchema =
        let toolName = ToolName.create "clarify_tool" |> Result.toOption |> Option.get
        {
            Name = toolName
            Description = "Ask the user one or more multiple-choice questions to resolve underspecified requirements or solicit design choices."
            ParametersJson = """{
                "type": "object",
                "properties": {
                    "question": { "type": "string", "description": "The question or design decision requiring user clarification." },
                    "options": { "type": "array", "items": { "type": "string" }, "description": "List of selectable options for the user." },
                    "is_multi_select": { "type": "boolean", "description": "Whether multiple options can be selected." }
                },
                "required": ["question", "options"]
            }"""
        }

    type ClarificationCallback = string -> string list -> bool -> Async<string>

    let createHandler (callback: ClarificationCallback option) : string -> Async<string> =
        fun (args: string) -> async {
            try
                use doc = JsonDocument.Parse(args)
                let root = doc.RootElement
                let question = root.GetProperty("question").GetString()
                let options =
                    root.GetProperty("options").EnumerateArray()
                    |> Seq.map (fun el -> el.GetString())
                    |> Seq.toList

                let isMultiSelect =
                    let mutable elem = JsonElement()
                    if root.TryGetProperty("is_multi_select", &elem) then
                        elem.GetBoolean()
                    else false

                match callback with
                | Some cb ->
                    let! userResponse = cb question options isMultiSelect
                    return sprintf "User selected: %s" userResponse
                | None ->
                    let defaultChoice = if options.Length > 0 then options.Head else "No option provided"
                    printfn "  [Clarify Tool] (Non-interactive mode) Defaulting to option: %s" defaultChoice
                    return sprintf "User selected (default): %s" defaultChoice
            with ex ->
                return sprintf "Error executing clarify_tool: %s" ex.Message
        }

    let register (registry: ToolRegistry) (callback: ClarificationCallback option) : unit =
        let handler = createHandler callback
        registry.Register("clarify_tool", schema.Description, handler, schema.ParametersJson)
