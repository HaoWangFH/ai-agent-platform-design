namespace Skight.AgentPlatform.FSharp

open System
open System.Text.Json
open System.Threading.Tasks

module DelegateTool =

    let delegateTask 
        (llmCaller: LlmCaller) 
        (executor: ToolExecutor) 
        (config: AgentConfig) 
        (schemas: ToolSchema list) 
        (namesSet: Set<ToolName>) 
        (argsJson: string) : Task<string> =
        
        async {
            try
                let doc = JsonDocument.Parse argsJson
                use _ = doc
                let root = doc.RootElement
                match root.TryGetProperty("role"), root.TryGetProperty("task") with
                | (true, roleProp), (true, taskProp) ->
                    let role = roleProp.GetString()
                    let taskDescription = taskProp.GetString()

                    let subConfig = { config with MaxIterations = 5 }
                    let systemPrompt = sprintf "You are a specialized subagent acting as: %s." role
                    let session = AgentSession.initialize systemPrompt

                    let! (result, _) = AgentRunner.runTurnAsync llmCaller executor subConfig taskDescription session schemas namesSet
                    match result.Outcome with
                    | Completed response -> return sprintf "Subagent (%s) output: %s" role response
                    | Interrupted -> return sprintf "Subagent (%s) was interrupted." role
                    | Failed _ -> return sprintf "Subagent (%s) failed." role
                | _ ->
                    return "Error: Missing 'role' or 'task' argument for delegate_task."
            with ex ->
                return sprintf "Error executing delegate_task: %s" ex.Message
        } |> Async.StartAsTask
