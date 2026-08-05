namespace Skight.AgentPlatform.FSharp

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions

module FileTools =
    let private defaultMaxOutputBytes = 100 * 1024

    let private tryGetProperty (name: string) (root: JsonElement) =
        let mutable prop = Unchecked.defaultof<JsonElement>
        if root.TryGetProperty(name, &prop) then Some prop else None

    let private getOptionalString (name: string) (root: JsonElement) =
        tryGetProperty name root
        |> Option.bind (fun p -> if p.ValueKind = JsonValueKind.String then Some (p.GetString()) else None)

    let private getRequiredString (name: string) (root: JsonElement) =
        match getOptionalString name root with
        | Some value when not (String.IsNullOrWhiteSpace(value)) -> Ok value
        | _ -> Error (sprintf "Missing required string property '%s'." name)

    let private getOptionalInt (name: string) (root: JsonElement) =
        match tryGetProperty name root with
        | Some p when p.ValueKind = JsonValueKind.Number ->
            match p.TryGetInt32() with
            | true, value -> Some value
            | _ -> None
        | _ -> None

    let private applySearchReplace (original: string) (searchText: string) (replaceText: string) =
        let index = original.IndexOf(searchText, StringComparison.Ordinal)
        if index < 0 then
            Error "Search text not found in file."
        else
            Ok (original.Remove(index, searchText.Length).Insert(index, replaceText), 1)

    let private diffBlockPattern = Regex("(?s)<<<<<<< SEARCH\s*(.*?)\s*=======\s*(.*?)\s*>>>>>>> REPLACE", RegexOptions.Compiled)

    let private applyDiffBlocks (original: string) (patchText: string) =
        let matches = diffBlockPattern.Matches(patchText)
        if matches.Count = 0 then
            Error "Patch format invalid. Expected one or more SEARCH/REPLACE blocks."
        else
            let mutable working = original
            let mutable replacements = 0
            let mutable failedSearch: string option = None

            for m in matches do
                let searchText = m.Groups.[1].Value
                let replaceText = m.Groups.[2].Value

                if failedSearch.IsNone then
                    let index = working.IndexOf(searchText, StringComparison.Ordinal)
                    if index < 0 then
                        failedSearch <- Some searchText
                    else
                        working <- working.Remove(index, searchText.Length).Insert(index, replaceText)
                        replacements <- replacements + 1

            match failedSearch with
            | Some missing ->
                let preview = if missing.Length > 120 then missing.Substring(0, 120) + "..." else missing
                Error (sprintf "Patch apply failed. SEARCH block not found: %s" preview)
            | None -> Ok (working, replacements)

    let readFileTool (workspaceRoot: string) (argsJson: string) : Async<string> =
        async {
            try
                use doc = JsonDocument.Parse(argsJson)
                let root = doc.RootElement

                match getRequiredString "path" root with
                | Error err -> return sprintf "Error: %s" err
                | Ok path ->
                    match ToolSecurity.validatePathInSandbox workspaceRoot path with
                    | Error err -> return err
                    | Ok safePath ->
                        if not (File.Exists(safePath)) then
                            return sprintf "Error: File '%s' not found." path
                        else
                            let! text = File.ReadAllTextAsync(safePath) |> Async.AwaitTask
                            let maxBytes = getOptionalInt "max_bytes" root |> Option.defaultValue defaultMaxOutputBytes
                            return ToolSecurity.truncateOutput maxBytes text
            with ex ->
                return sprintf "Error reading file: %s" ex.Message
        }

    let writeFileTool (workspaceRoot: string) (argsJson: string) : Async<string> =
        async {
            try
                use doc = JsonDocument.Parse(argsJson)
                let root = doc.RootElement

                match getRequiredString "path" root, getOptionalString "content" root with
                | Error err, _ -> return sprintf "Error: %s" err
                | _, None -> return "Error: Missing required string property 'content'."
                | Ok path, Some content ->
                    match ToolSecurity.validatePathInSandbox workspaceRoot path with
                    | Error err -> return err
                    | Ok safePath ->
                        let directory = Path.GetDirectoryName(safePath)
                        if not (String.IsNullOrWhiteSpace(directory)) then
                            Directory.CreateDirectory(directory) |> ignore

                        do! File.WriteAllTextAsync(safePath, content) |> Async.AwaitTask
                        return sprintf "Wrote %d characters to '%s'." content.Length path
            with ex ->
                return sprintf "Error writing file: %s" ex.Message
        }

    let editFileTool (workspaceRoot: string) (argsJson: string) : Async<string> =
        async {
            try
                use doc = JsonDocument.Parse(argsJson)
                let root = doc.RootElement

                match getRequiredString "path" root with
                | Error err -> return sprintf "Error: %s" err
                | Ok path ->
                    match ToolSecurity.validatePathInSandbox workspaceRoot path with
                    | Error err -> return err
                    | Ok safePath ->
                        if not (File.Exists(safePath)) then
                            return sprintf "Error: File '%s' not found." path
                        else
                            let! original = File.ReadAllTextAsync(safePath) |> Async.AwaitTask

                            let result =
                                match getOptionalString "search" root, getOptionalString "replace" root with
                                | Some searchText, Some replaceText when not (String.IsNullOrEmpty(searchText)) ->
                                    applySearchReplace original searchText replaceText
                                | _ ->
                                    match getOptionalString "patch" root with
                                    | Some patchText when not (String.IsNullOrWhiteSpace(patchText)) ->
                                        applyDiffBlocks original patchText
                                    | _ ->
                                        Error "Edit requires either search+replace or patch arguments."

                            match result with
                            | Error err -> return sprintf "Error: %s" err
                            | Ok (updated, count) ->
                                do! File.WriteAllTextAsync(safePath, updated) |> Async.AwaitTask
                                return sprintf "Applied %d edit(s) to '%s'." count path
            with ex ->
                return sprintf "Error editing file: %s" ex.Message
        }
