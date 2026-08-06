namespace Skight.AgentPlatform.FSharp

open System
open System.Text.Json
open System.Threading.Tasks

module GitTools =

    let gitStatus (workspaceRoot: string) : Task<string> =
        TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" status" workspaceRoot)
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
                    let! addResult = TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" add ." workspaceRoot)
                    if addResult.StartsWith("Error") then
                        return addResult
                    else
                        return! TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" commit -m \"%s\"" workspaceRoot message)
                | false, _ ->
                    return "Error: Missing 'message' argument for git_commit."
            with ex ->
                return sprintf "Error executing git_commit: %s" ex.Message
        } |> Async.StartAsTask

    let gitPush (workspaceRoot: string) : Task<string> =
        TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" push" workspaceRoot)
        |> Async.StartAsTask
