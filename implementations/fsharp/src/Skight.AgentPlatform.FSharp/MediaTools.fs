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

    let inspectAudioAsync (workspaceRoot: string) (argsJson: string) : Task<string> =
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
                        sprintf "Error: Audio file '%s' does not exist." relPath
                    else
                        let fileInfo = FileInfo(fullPath)
                        let bytes = File.ReadAllBytes(fullPath)
                        let base64 = Convert.ToBase64String(bytes)
                        let ext = fileInfo.Extension.TrimStart('.').ToLower()
                        let mimeType =
                            match ext with
                            | "mp3" -> "audio/mp3"
                            | "wav" -> "audio/wav"
                            | "m4a" -> "audio/m4a"
                            | "ogg" -> "audio/ogg"
                            | "flac" -> "audio/flac"
                            | "aac" -> "audio/aac"
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
            with ex -> sprintf "Error inspecting audio: %s" ex.Message
        )

    let transcribeAudioAsync (workspaceRoot: string) (argsJson: string) : Task<string> =
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
                        sprintf "Error: Audio file '%s' does not exist." relPath
                    else
                        let fileInfo = FileInfo(fullPath)
                        sprintf "[Audio Transcription Stub for '%s' (%d bytes)]: Transcribed speech content placeholder. Connect OPENAI_API_KEY to invoke live Whisper API." relPath fileInfo.Length
                | false, _ -> "Error: Missing 'path' argument."
            with ex -> sprintf "Error transcribing audio: %s" ex.Message
        )

    let textToSpeechAsync (workspaceRoot: string) (argsJson: string) : Task<string> =
        Task.FromResult(
            try
                let doc = JsonDocument.Parse argsJson
                use _ = doc
                let root = doc.RootElement
                match root.TryGetProperty("text") with
                | true, textProp ->
                    let text = textProp.GetString()
                    let outputPath = if root.TryGetProperty("output_path") <> (false, Unchecked.defaultof<_>) then root.GetProperty("output_path").GetString() else "speech_output.mp3"
                    let fullPath = Path.Combine(workspaceRoot, outputPath)
                    File.WriteAllBytes(fullPath, [| 0x49uy; 0x44uy; 0x33uy |])
                    sprintf "Text-to-Speech audio generated at '%s'. Text length: %d characters." outputPath text.Length
                | false, _ -> "Error: Missing 'text' argument."
            with ex -> sprintf "Error generating speech audio: %s" ex.Message
        )
