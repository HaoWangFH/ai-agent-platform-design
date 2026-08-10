namespace Skight.AgentPlatform.FSharp

open System
open System.IO
open System.Text.Json

type TelemetryEventType =
    | SessionStart
    | TurnStart
    | ContextCompaction
    | LlmCall
    | ToolExecution
    | TurnEnd

type FSharpTelemetryEvent = {
    EventId: string
    SessionId: string
    UserId: string
    TurnIndex: int
    EventType: string
    Timestamp: DateTime
    DurationMs: int64
    Name: string
    Payload: string
    RawPayload: string
}

type TelemetryMessage =
    | EventMessage of FSharpTelemetryEvent
    | FlushMessage of AsyncReplyChannel<unit>

module AgentTelemetry =

    let mutable IsEnabled = true
    let mutable LogDirectory = "logs/transcripts"

    let private writeToFile (evt: FSharpTelemetryEvent) =
        try
            if not (String.IsNullOrWhiteSpace evt.SessionId) then
                let dir = Path.Combine(LogDirectory, evt.SessionId)
                if not (Directory.Exists dir) then Directory.CreateDirectory dir |> ignore

                let compactPath = Path.Combine(dir, "transcript.jsonl")
                let fullPath = Path.Combine(dir, "transcript_full.jsonl")

                let compactObj = {|
                    EventId = evt.EventId
                    SessionId = evt.SessionId
                    UserId = evt.UserId
                    TurnIndex = evt.TurnIndex
                    EventType = evt.EventType
                    Timestamp = evt.Timestamp
                    DurationMs = evt.DurationMs
                    Name = evt.Name
                    Payload = evt.Payload
                |}

                let compactJson = JsonSerializer.Serialize compactObj
                let fullJson = JsonSerializer.Serialize evt

                File.AppendAllText(compactPath, compactJson + "\n")
                File.AppendAllText(fullPath, fullJson + "\n")
        with ex ->
            Console.WriteLine(sprintf "Telemetry Write Error: %s" ex.Message)

    let private agent =
        MailboxProcessor<TelemetryMessage>.Start(fun inbox ->
            let rec loop () = async {
                let! msg = inbox.Receive()
                match msg with
                | EventMessage evt ->
                    writeToFile evt
                    return! loop ()
                | FlushMessage reply ->
                    reply.Reply()
                    return! loop ()
            }
            loop ()
        )

    let track (evt: FSharpTelemetryEvent) =
        if IsEnabled then
            agent.Post(EventMessage evt)

    let flush () =
        agent.PostAndReply(fun reply -> FlushMessage reply)

    let trackTurnStart (sessionId: string) (userId: string) (turnIndex: int) (userInput: string) =
        if IsEnabled then
            track {
                EventId = Guid.NewGuid().ToString("N")
                SessionId = sessionId
                UserId = userId
                TurnIndex = turnIndex
                EventType = "TurnStart"
                Timestamp = DateTime.UtcNow
                DurationMs = 0L
                Name = "agent.turn.start"
                Payload = userInput
                RawPayload = userInput
            }

    let trackToolExecution (sessionId: string) (userId: string) (turnIndex: int) (toolName: string) (durationMs: int64) (argsJson: string) (result: string) =
        if IsEnabled then
            track {
                EventId = Guid.NewGuid().ToString("N")
                SessionId = sessionId
                UserId = userId
                TurnIndex = turnIndex
                EventType = "ToolExecution"
                Timestamp = DateTime.UtcNow
                DurationMs = durationMs
                Name = sprintf "tool.execution:%s" toolName
                Payload = sprintf "Tool: %s, ResultLength: %d" toolName result.Length
                RawPayload = sprintf "Args: %s\nResult: %s" argsJson result
            }

    let trackTurnEnd (sessionId: string) (userId: string) (turnIndex: int) (durationMs: int64) (finalResponse: string) (exitReason: string) =
        if IsEnabled then
            track {
                EventId = Guid.NewGuid().ToString("N")
                SessionId = sessionId
                UserId = userId
                TurnIndex = turnIndex
                EventType = "TurnEnd"
                Timestamp = DateTime.UtcNow
                DurationMs = durationMs
                Name = "agent.turn.end"
                Payload = sprintf "ExitReason: %s, ResponseLength: %d" exitReason finalResponse.Length
                RawPayload = finalResponse
            }
