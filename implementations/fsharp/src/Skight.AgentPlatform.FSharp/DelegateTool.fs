namespace Skight.AgentPlatform.FSharp

open System
open System.Text.Json
open System.Threading.Tasks

module DelegateTool =

    let private filterLeafSchemas (schemas: ToolSchema list) (namesSet: Set<ToolName>) =
        let delegateName = ToolName.create "delegate_task" |> Result.toOption
        match delegateName with
        | Some dName ->
            let filteredSchemas = schemas |> List.filter (fun s -> s.Name <> dName)
            let filteredNamesSet = namesSet |> Set.remove dName
            filteredSchemas, filteredNamesSet
        | None -> schemas, namesSet

    let runSingleSubAgentAsync
        (llmCaller: LlmCaller)
        (executor: ToolExecutor)
        (config: AgentConfig)
        (schemas: ToolSchema list)
        (namesSet: Set<ToolName>)
        (role: string)
        (taskDesc: string) : Async<string> =
        async {
            let leafSchemas, leafNamesSet = filterLeafSchemas schemas namesSet
            let subConfig = { config with MaxIterations = 5 }
            let systemPrompt = sprintf "You are a specialized subagent acting as: %s." role
            let session = AgentSession.initialize systemPrompt

            let! (result, _) = AgentRunner.runTurnAsync llmCaller executor subConfig taskDesc session leafSchemas leafNamesSet
            match result.Outcome with
            | Completed response -> return sprintf "Subagent (%s) output: %s" role response
            | Interrupted -> return sprintf "Subagent (%s) was interrupted." role
            | Failed reason -> return sprintf "Subagent (%s) failed: %A" role reason
        }

    let delegateTask
        (llmCaller: LlmCaller)
        (executor: ToolExecutor)
        (config: AgentConfig)
        (schemas: ToolSchema list)
        (namesSet: Set<ToolName>)
        (argsJson: string) : Task<string> =
        
        async {
            try
                use doc = JsonDocument.Parse argsJson
                let root = doc.RootElement
                let mutable tasksElem = JsonElement()

                if root.TryGetProperty("tasks", &tasksElem) && tasksElem.ValueKind = JsonValueKind.Array then
                    let taskWork = [
                        for t in tasksElem.EnumerateArray() do
                            let mutable rElem = JsonElement()
                            let mutable tElem = JsonElement()
                            let roleStr = if t.TryGetProperty("role", &rElem) then rElem.GetString() else "leaf"
                            let taskStr = if t.TryGetProperty("task", &tElem) then tElem.GetString() elif t.TryGetProperty("goal", &tElem) then tElem.GetString() else ""

                            if not (String.IsNullOrWhiteSpace taskStr) then
                                yield runSingleSubAgentAsync llmCaller executor config schemas namesSet roleStr taskStr
                    ]

                    if taskWork.IsEmpty then
                        return "Error: Empty 'tasks' array provided for delegate_task."
                    else
                        let! results = Async.Parallel taskWork
                        let combined = String.concat "\n---\n" results
                        return sprintf "Batch Subagent Execution Results:\n%s" combined

                elif root.TryGetProperty("task", &tasksElem) || root.TryGetProperty("goal", &tasksElem) then
                    let taskDesc = tasksElem.GetString()
                    let mutable roleElem = JsonElement()
                    let roleStr = if root.TryGetProperty("role", &roleElem) then roleElem.GetString() else "leaf"
                    return! runSingleSubAgentAsync llmCaller executor config schemas namesSet roleStr taskDesc
                else
                    return "Error: Missing 'task', 'goal', or 'tasks' argument for delegate_task."
            with ex ->
                return sprintf "Error executing delegate_task: %s" ex.Message
        } |> Async.StartAsTask
