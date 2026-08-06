namespace Skight.AgentPlatform.FSharp

open System
open System.Collections.Generic
open System.Text
open System.Text.Json
open System.Threading.Tasks

type TodoItem = {
    Id: int
    Description: string
    mutable IsCompleted: bool
}

module TodoTool =

    let private todos = List<TodoItem>()
    let mutable private nextId = 1

    let addTodoAsync (argsJson: string) : Task<string> =
        Task.FromResult(
            lock todos (fun () ->
                try
                    let doc = JsonDocument.Parse argsJson
                    use _ = doc
                    let root = doc.RootElement
                    match root.TryGetProperty("task") with
                    | true, taskProp ->
                        let item = { Id = nextId; Description = taskProp.GetString(); IsCompleted = false }
                        nextId <- nextId + 1
                        todos.Add(item)
                        sprintf "Added TODO #%d: %s" item.Id item.Description
                    | false, _ -> "Error: Missing 'task' argument."
                with ex -> sprintf "Error adding TODO: %s" ex.Message
            )
        )

    let listTodosAsync () : Task<string> =
        Task.FromResult(
            lock todos (fun () ->
                if todos.Count = 0 then "TODO list is empty."
                else
                    let sb = StringBuilder("Current TODO List:\n")
                    for todo in todos do
                        let status = if todo.IsCompleted then "[x]" else "[ ]"
                        sb.AppendLine(sprintf "#%d %s %s" todo.Id status todo.Description) |> ignore
                    sb.ToString().TrimEnd()
            )
        )

    let completeTodoAsync (argsJson: string) : Task<string> =
        Task.FromResult(
            lock todos (fun () ->
                try
                    let doc = JsonDocument.Parse argsJson
                    use _ = doc
                    let root = doc.RootElement
                    match root.TryGetProperty("id") with
                    | true, idProp ->
                        let id = idProp.GetInt32()
                        match todos |> Seq.tryFind (fun t -> t.Id = id) with
                        | Some todo ->
                            todo.IsCompleted <- true
                            sprintf "Completed TODO #%d: %s" id todo.Description
                        | None -> sprintf "Error: TODO #%d not found." id
                    | false, _ -> "Error: Missing 'id' argument."
                with ex -> sprintf "Error completing TODO: %s" ex.Message
            )
        )
