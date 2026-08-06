namespace Skight.AgentPlatform.FSharp

open System
open System.Text.Json
open System.Threading.Tasks

module AutomationTools =

    let scheduleTimerAsync (argsJson: string) : Task<string> =
        Task.FromResult(
            try
                let doc = JsonDocument.Parse argsJson
                use _ = doc
                let root = doc.RootElement
                match root.TryGetProperty("seconds"), root.TryGetProperty("prompt") with
                | (true, secProp), (true, promptProp) ->
                    let seconds = secProp.GetInt32()
                    let prompt = promptProp.GetString()
                    let taskId = Guid.NewGuid().ToString("N").Substring(0, 8)

                    Task.Run(fun () ->
                        async {
                            do! Async.Sleep (seconds * 1000)
                            printfn "\n  [Automation Timer Fired] Task #%s: '%s'" taskId prompt
                        } |> Async.StartAsTask :> Task
                    ) |> ignore

                    sprintf "Timer task #%s scheduled for %d seconds. Prompt: '%s'" taskId seconds prompt
                | _ -> "Error: Missing 'seconds' or 'prompt' argument."
            with ex -> sprintf "Error scheduling timer: %s" ex.Message
        )
