namespace Skight.AgentPlatform.FSharp

open System
open System.IO
open System.Text.Json
open System.Diagnostics
open OpenTelemetry
open OpenTelemetry.Trace

type TelemetryEventType =
    | SessionStart
    | TurnStart
    | ContextCompaction
    | LlmCall
    | ToolExecution
    | TurnEnd

type FSharpTelemetryEvent = {
    EventId: string
    TraceId: string
    SpanId: string
    ParentSpanId: string option
    SessionId: string
    UserId: string
    TurnIndex: int
    EventType: string
    Timestamp: DateTime
    DurationMs: int64
    Name: string
    Payload: string
    RawPayload: string
    IsError: bool
    ExceptionDetails: string option
}

type TelemetryMessage =
    | EventMessage of FSharpTelemetryEvent
    | FlushMessage of AsyncReplyChannel<unit>

module AgentTelemetry =

    let mutable IsEnabled = true
    let mutable LogDirectory = "logs/transcripts"
    let mutable OtlpEndpoint = "http://localhost:4317"
    let mutable private tracerProvider: TracerProvider option = None
    let activitySource = new ActivitySource("Skight.AgentPlatform.FSharp", "1.0.0")

    let initOpenTelemetry (endpoint: string option) =
        if IsEnabled && tracerProvider.IsNone then
            try
                let envEp = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                let ep = 
                    if not (String.IsNullOrWhiteSpace envEp) then envEp
                    else defaultArg endpoint OtlpEndpoint

                let builder =
                    Sdk.CreateTracerProviderBuilder()
                        .AddSource("Skight.AgentPlatform.FSharp")
                        .AddOtlpExporter(fun opt -> opt.Endpoint <- Uri(ep))
                tracerProvider <- Some(builder.Build())
            with ex ->
                Console.WriteLine(sprintf "OTel Init Warning: %s" ex.Message)

    let private writeToFile (evt: FSharpTelemetryEvent) =
        try
            if not (String.IsNullOrWhiteSpace evt.SessionId) then
                let dir = Path.Combine(LogDirectory, evt.SessionId)
                if not (Directory.Exists dir) then Directory.CreateDirectory dir |> ignore

                let compactPath = Path.Combine(dir, "transcript.jsonl")
                let fullPath = Path.Combine(dir, "transcript_full.jsonl")

                let compactObj = {|
                    EventId = evt.EventId
                    TraceId = evt.TraceId
                    SpanId = evt.SpanId
                    ParentSpanId = match evt.ParentSpanId with Some p -> p | None -> null
                    SessionId = evt.SessionId
                    UserId = evt.UserId
                    TurnIndex = evt.TurnIndex
                    EventType = evt.EventType
                    Timestamp = evt.Timestamp
                    DurationMs = evt.DurationMs
                    Name = evt.Name
                    Payload = evt.Payload
                    IsError = evt.IsError
                    ExceptionDetails = match evt.ExceptionDetails with Some ex -> ex | None -> null
                |}

                let jsonOptions = JsonSerializerOptions(Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
                let compactJson = JsonSerializer.Serialize(compactObj, jsonOptions)
                let fullJson = JsonSerializer.Serialize(evt, jsonOptions)

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

    let toW3cTraceId (idStr: string) =
        let clean = if isNull idStr then "" else idStr.Replace("-", "")
        if clean.Length >= 32 then clean.Substring(0, 32)
        else clean.PadLeft(32, '0')

    let toW3cSpanId (idStr: string) =
        let clean = if isNull idStr then "" else idStr.Replace("-", "")
        if clean.Length >= 16 then clean.Substring(0, 16)
        else clean.PadLeft(16, '0')

    let track (evt: FSharpTelemetryEvent) =
        if IsEnabled then
            initOpenTelemetry None
            agent.Post(EventMessage evt)
            try
                let mutable parentContext = ActivityContext()
                match evt.ParentSpanId with
                | Some parentSpanIdStr when not (String.IsNullOrWhiteSpace evt.TraceId) ->
                    try
                        let traceId = ActivityTraceId.CreateFromString(toW3cTraceId(evt.TraceId).AsSpan())
                        let spanId = ActivitySpanId.CreateFromString(toW3cSpanId(parentSpanIdStr).AsSpan())
                        parentContext <- ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded)
                    with _ -> ()
                | _ -> ()

                let activity =
                    if parentContext <> ActivityContext() then
                        activitySource.StartActivity(evt.Name, ActivityKind.Internal, parentContext)
                    else
                        activitySource.StartActivity(evt.Name, ActivityKind.Internal)

                if not (isNull activity) then
                    activity.SetTag("gen_ai.session.id", evt.SessionId) |> ignore
                    activity.SetTag("gen_ai.user.id", evt.UserId) |> ignore
                    activity.SetTag("gen_ai.turn.index", evt.TurnIndex) |> ignore
                    activity.SetTag("event.type", evt.EventType) |> ignore
                    activity.SetTag("payload", evt.Payload) |> ignore

                    if evt.IsError then
                        activity.SetStatus(ActivityStatusCode.Error, evt.Payload) |> ignore
                        match evt.ExceptionDetails with
                        | Some exStr -> activity.SetTag("exception.stacktrace", exStr) |> ignore
                        | None -> ()

                    if evt.DurationMs > 0L then
                        activity.SetEndTime(activity.StartTimeUtc.AddMilliseconds(float evt.DurationMs)) |> ignore

                    activity.Stop()
                    activity.Dispose()
            with _ -> ()

    let flush () =
        agent.PostAndReply(fun reply -> FlushMessage reply)
        match tracerProvider with
        | Some tp -> tp.ForceFlush() |> ignore
        | None -> ()

    let trackTurnStart (sessionId: string) (userId: string) (turnIndex: int) (userInput: string) (traceId: string option) (spanId: string option) =
        if IsEnabled then
            let tid = defaultArg traceId sessionId
            let sid = defaultArg spanId (Guid.NewGuid().ToString("N"))
            track {
                EventId = Guid.NewGuid().ToString("N")
                TraceId = tid
                SpanId = sid
                ParentSpanId = None
                SessionId = sessionId
                UserId = userId
                TurnIndex = turnIndex
                EventType = "TurnStart"
                Timestamp = DateTime.UtcNow
                DurationMs = 0L
                Name = "agent.turn.start"
                Payload = userInput
                RawPayload = userInput
                IsError = false
                ExceptionDetails = None
            }

    let trackLlmCall (sessionId: string) (userId: string) (turnIndex: int) (model: string) (durationMs: int64) (responseContent: string) (toolCalls: ToolCall list) (traceId: string option) (parentSpanId: string option) =
        if IsEnabled then
            let toolDetails = toolCalls |> List.map (fun tc -> sprintf "%s(%s)" (ToolName.value tc.Name) tc.ArgumentsJson)
            let toolSummary = if toolDetails.IsEmpty then "" else sprintf " Requested ToolCalls: [%s]" (String.concat ", " toolDetails)
            let payloadText = if String.IsNullOrEmpty responseContent then sprintf "Model: %s%s" model toolSummary else sprintf "Model: %s, Content: %s%s" model responseContent toolSummary
            track {
                EventId = Guid.NewGuid().ToString("N")
                TraceId = defaultArg traceId sessionId
                SpanId = Guid.NewGuid().ToString("N")
                ParentSpanId = parentSpanId
                SessionId = sessionId
                UserId = userId
                TurnIndex = turnIndex
                EventType = "LlmCall"
                Timestamp = DateTime.UtcNow
                DurationMs = durationMs
                Name = "llm.call"
                Payload = payloadText
                RawPayload = sprintf "Content: %s\nToolCalls:\n%s" responseContent (String.concat "\n" toolDetails)
                IsError = false
                ExceptionDetails = None
            }

    let trackToolExecution (sessionId: string) (userId: string) (turnIndex: int) (toolName: string) (durationMs: int64) (argsJson: string) (result: string) (traceId: string option) (parentSpanId: string option) (isError: bool option) (exceptionDetails: string option) =
        if IsEnabled then
            let err = defaultArg isError false
            let payloadText = if err then sprintf "Tool '%s' Error: %s" toolName result else sprintf "Args: %s => Result: %s" argsJson result
            track {
                EventId = Guid.NewGuid().ToString("N")
                TraceId = defaultArg traceId sessionId
                SpanId = Guid.NewGuid().ToString("N")
                ParentSpanId = parentSpanId
                SessionId = sessionId
                UserId = userId
                TurnIndex = turnIndex
                EventType = "ToolExecution"
                Timestamp = DateTime.UtcNow
                DurationMs = durationMs
                Name = sprintf "tool.execution:%s" toolName
                Payload = payloadText
                RawPayload = sprintf "Args: %s\nResult: %s" argsJson result
                IsError = err
                ExceptionDetails = exceptionDetails
            }

    let trackTurnEnd (sessionId: string) (userId: string) (turnIndex: int) (durationMs: int64) (finalResponse: string) (exitReason: string) (traceId: string option) (spanId: string option) =
        if IsEnabled then
            track {
                EventId = Guid.NewGuid().ToString("N")
                TraceId = defaultArg traceId sessionId
                SpanId = defaultArg spanId (Guid.NewGuid().ToString("N"))
                ParentSpanId = None
                SessionId = sessionId
                UserId = userId
                TurnIndex = turnIndex
                EventType = "TurnEnd"
                Timestamp = DateTime.UtcNow
                DurationMs = durationMs
                Name = "agent.turn.end"
                Payload = sprintf "ExitReason: %s, ResponseLength: %d" exitReason finalResponse.Length
                RawPayload = finalResponse
                IsError = (exitReason <> "completed" && exitReason <> "text_response")
                ExceptionDetails = None
            }
