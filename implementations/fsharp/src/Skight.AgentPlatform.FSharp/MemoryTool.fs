namespace Skight.AgentPlatform.FSharp

open System
open System.Collections.Concurrent
open System.Text.Json
open System.Threading.Tasks

module MemoryTool =

    let private store = ConcurrentDictionary<string, string>()

    let storeMemoryAsync (argsJson: string) : Task<string> =
        Task.FromResult(
            try
                let doc = JsonDocument.Parse argsJson
                use _ = doc
                let root = doc.RootElement
                match root.TryGetProperty("key"), root.TryGetProperty("value") with
                | (true, keyProp), (true, valProp) ->
                    let key = keyProp.GetString()
                    let value = valProp.GetString()
                    store.[key] <- value
                    sprintf "Memory stored for key '%s'." key
                | _ -> "Error: Missing 'key' or 'value' argument."
            with ex -> sprintf "Error storing memory: %s" ex.Message
        )

    let recallMemoryAsync (argsJson: string) : Task<string> =
        Task.FromResult(
            try
                let doc = JsonDocument.Parse argsJson
                use _ = doc
                let root = doc.RootElement
                match root.TryGetProperty("key") with
                | true, keyProp ->
                    let key = keyProp.GetString()
                    match store.TryGetValue(key) with
                    | true, valStr -> sprintf "Memory '%s': %s" key valStr
                    | false, _ -> sprintf "Memory '%s' not found." key
                | false, _ -> "Error: Missing 'key' argument."
            with ex -> sprintf "Error recalling memory: %s" ex.Message
        )
