namespace Skight.AgentPlatform.FSharp

open System
open System.IO
open System.Text.Json
open System.Threading.Tasks

module MediaTools =

    let inspectImageAsync (workspaceRoot: string) (argsJson: string) : Task<string> =
        Task.FromResult(
            try
                let doc = JsonDocument.Parse argsJson
                use _ = doc
                let root = doc.RootElement
                match root.TryGetProperty("path") with
                | true, pathProp ->
                    let relPath = pathProp.GetString()
                    let fullPath = Path.Combine(workspaceRoot, relPath)
                    if not (File.Exists(fullPath)) then
                        sprintf "Error: Image file '%s' does not exist." relPath
                    else
                        let fileInfo = FileInfo(fullPath)
                        let bytes = File.ReadAllBytes(fullPath)
                        let base64 = Convert.ToBase64String(bytes)
                        let ext = fileInfo.Extension.TrimStart('.').ToLower()
                        let mimeType =
                            match ext with
                            | "png" -> "image/png"
                            | "jpg" | "jpeg" -> "image/jpeg"
                            | "gif" -> "image/gif"
                            | "webp" -> "image/webp"
                            | "svg" -> "image/svg+xml"
                            | _ -> "application/octet-stream"

                        let preview = if base64.Length > 100 then base64.Substring(0, 100) + "..." else base64
                        let result = {|
                            path = relPath
                            sizeBytes = fileInfo.Length
                            mimeType = mimeType
                            base64Preview = preview
                            dataUri = sprintf "data:%s;base64,%s" mimeType base64
                        |}

                        let options = JsonSerializerOptions(WriteIndented = true)
                        JsonSerializer.Serialize(result, options)
                | false, _ -> "Error: Missing 'path' argument."
            with ex -> sprintf "Error inspecting image: %s" ex.Message
        )
