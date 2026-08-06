namespace Skight.AgentPlatform.FSharp

open System
open System.Text.Json
open System.Threading.Tasks

module GitTools =

    let private normalizePath (path: string) = path.Replace('\\', '/')

    let gitStatus (workspaceRoot: string) : Task<string> =
        let path = normalizePath workspaceRoot
        TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" status" path)
        |> Async.StartAsTask

    let gitCommit (workspaceRoot: string) (argsJson: string) : Task<string> =
        async {
            try
                let doc = JsonDocument.Parse argsJson
                use _ = doc
                let root = doc.RootElement
                match root.TryGetProperty("message") with
                | true, msgProp ->
                    let message = msgProp.GetString()
                    let path = normalizePath workspaceRoot
                    let! addResult = TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" add ." path)
                    if addResult.StartsWith("Error") then
                        return addResult
                    else
                        return! TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" commit -m \"%s\"" path message)
                | false, _ ->
                    return "Error: Missing 'message' argument for git_commit."
            with ex ->
                return sprintf "Error executing git_commit: %s" ex.Message
        } |> Async.StartAsTask

    let gitPush (workspaceRoot: string) : Task<string> =
        let path = normalizePath workspaceRoot
        TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" push" path)
        |> Async.StartAsTask

    let gitDiff (workspaceRoot: string) : Task<string> =
        let path = normalizePath workspaceRoot
        TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" diff" path)
        |> Async.StartAsTask

    let gitLog (workspaceRoot: string) (argsJson: string) : Task<string> =
        async {
            let mutable count = 5
            try
                let doc = JsonDocument.Parse (if String.IsNullOrWhiteSpace argsJson then "{}" else argsJson)
                use _ = doc
                let root = doc.RootElement
                match root.TryGetProperty("count") with
                | true, countProp -> count <- countProp.GetInt32()
                | false, _ -> ()
            with _ -> ()

            let path = normalizePath workspaceRoot
            return! TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" log -n %d --oneline" path count)
        } |> Async.StartAsTask
