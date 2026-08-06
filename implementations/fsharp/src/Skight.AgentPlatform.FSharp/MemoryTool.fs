namespace Skight.AgentPlatform.FSharp

open System
open System.Collections.Concurrent
open System.IO
open System.Text.Json
open System.Threading.Tasks

module MemoryTool =

    let private memoryFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".agent_memory.json")
    let private store = ConcurrentDictionary<string, string>()

    let private loadFromDisk () =
        try
            if File.Exists(memoryFilePath) then
                let json = File.ReadAllText(memoryFilePath)
                let dict = JsonSerializer.Deserialize<ConcurrentDictionary<string, string>>(json)
                if not (isNull dict) then
                    for kvp in dict do
                        store.[kvp.Key] <- kvp.Value
        with _ -> ()

    let private saveToDisk () =
        try
            let options = JsonSerializerOptions(WriteIndented = true)
            let json = JsonSerializer.Serialize(store, options)
            File.WriteAllText(memoryFilePath, json)
        with _ -> ()

    // Initialize by loading from disk on startup
    do loadFromDisk ()

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
                    saveToDisk ()
                    sprintf "Memory stored for key '%s'. (Saved to disk: .agent_memory.json)" key
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
