namespace Skight.AgentPlatform.FSharp

open System
open System.IO
open System.Text.Json
open System.Diagnostics
open OpenTelemetry
open OpenTelemetry.Trace

open System.Collections.Concurrent
open OpenTelemetry.Resources

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
    let private activeTurnActivities = ConcurrentDictionary<string, Activity>()

    let initOpenTelemetry (endpoint: string option) =
        if IsEnabled && tracerProvider.IsNone then
            try
                let envEp = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                let ep = 
                    if not (String.IsNullOrWhiteSpace envEp) then envEp
                    else defaultArg endpoint OtlpEndpoint

                let envSvcName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")
                let serviceName = if not (String.IsNullOrWhiteSpace envSvcName) then envSvcName else "fsharp-agent-platform"

                let builder =
                    Sdk.CreateTracerProviderBuilder()
                        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
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

    let flush () =
        agent.PostAndReply(fun reply -> FlushMessage reply)
        match tracerProvider with
        | Some tp -> tp.ForceFlush() |> ignore
        | None -> ()

    let trackTurnStart (sessionId: string) (userId: string) (turnIndex: int) (userInput: string) (traceId: string option) (spanId: string option) =
        if IsEnabled then
            initOpenTelemetry None
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

            try
                let act = activitySource.StartActivity("agent.turn", ActivityKind.Internal)
                if not (isNull act) then
                    act.SetTag("gen_ai.session.id", sessionId) |> ignore
                    act.SetTag("gen_ai.user.id", userId) |> ignore
                    act.SetTag("gen_ai.turn.index", turnIndex) |> ignore
                    act.SetTag("payload", userInput) |> ignore
                    activeTurnActivities.[sid] <- act
            with _ -> ()

    let trackLlmCall (sessionId: string) (userId: string) (turnIndex: int) (model: string) (durationMs: int64) (responseContent: string) (toolCalls: ToolCall list) (traceId: string option) (parentSpanId: string option) =
        if IsEnabled then
            initOpenTelemetry None
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

            try
                let mutable parentContext = ActivityContext()
                match parentSpanId with
                | Some pid when activeTurnActivities.ContainsKey(pid) ->
                    parentContext <- activeTurnActivities.[pid].Context
                | _ -> ()

                let activity =
                    if parentContext <> ActivityContext() then
                        activitySource.StartActivity("llm.call", ActivityKind.Internal, parentContext)
                    else
                        activitySource.StartActivity("llm.call", ActivityKind.Internal)

                if not (isNull activity) then
                    activity.SetTag("gen_ai.session.id", sessionId) |> ignore
                    activity.SetTag("gen_ai.user.id", userId) |> ignore
                    activity.SetTag("gen_ai.turn.index", turnIndex) |> ignore
                    activity.SetTag("payload", payloadText) |> ignore
                    if durationMs > 0L then
                        activity.SetEndTime(activity.StartTimeUtc.AddMilliseconds(float durationMs)) |> ignore
                    activity.Stop()
                    activity.Dispose()
            with _ -> ()

    let trackToolExecution (sessionId: string) (userId: string) (turnIndex: int) (toolName: string) (durationMs: int64) (argsJson: string) (result: string) (traceId: string option) (parentSpanId: string option) (isError: bool option) (exceptionDetails: string option) =
        if IsEnabled then
            initOpenTelemetry None
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

            try
                let mutable parentContext = ActivityContext()
                match parentSpanId with
                | Some pid when activeTurnActivities.ContainsKey(pid) ->
                    parentContext <- activeTurnActivities.[pid].Context
                | _ -> ()

                let actName = sprintf "tool.execution:%s" toolName
                let activity =
                    if parentContext <> ActivityContext() then
                        activitySource.StartActivity(actName, ActivityKind.Internal, parentContext)
                    else
                        activitySource.StartActivity(actName, ActivityKind.Internal)

                if not (isNull activity) then
                    activity.SetTag("gen_ai.session.id", sessionId) |> ignore
                    activity.SetTag("gen_ai.user.id", userId) |> ignore
                    activity.SetTag("gen_ai.turn.index", turnIndex) |> ignore
                    activity.SetTag("payload", payloadText) |> ignore
                    if err then
                        activity.SetStatus(ActivityStatusCode.Error, payloadText) |> ignore
                    if durationMs > 0L then
                        activity.SetEndTime(activity.StartTimeUtc.AddMilliseconds(float durationMs)) |> ignore
                    activity.Stop()
                    activity.Dispose()
            with _ -> ()

    let trackTurnEnd (sessionId: string) (userId: string) (turnIndex: int) (durationMs: int64) (finalResponse: string) (exitReason: string) (traceId: string option) (spanId: string option) =
        if IsEnabled then
            initOpenTelemetry None
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
                EventType = "TurnEnd"
                Timestamp = DateTime.UtcNow
                DurationMs = durationMs
                Name = "agent.turn.end"
                Payload = sprintf "ExitReason: %s, ResponseLength: %d" exitReason finalResponse.Length
                RawPayload = finalResponse
                IsError = (exitReason <> "completed" && exitReason <> "text_response")
                ExceptionDetails = None
            }

            try
                match activeTurnActivities.TryRemove(sid) with
                | true, act when not (isNull act) ->
                    act.SetTag("payload", sprintf "ExitReason: %s, ResponseLength: %d" exitReason finalResponse.Length) |> ignore
                    if exitReason <> "completed" && exitReason <> "text_response" then
                        act.SetStatus(ActivityStatusCode.Error, sprintf "ExitReason: %s" exitReason) |> ignore
                    if durationMs > 0L then
                        act.SetEndTime(act.StartTimeUtc.AddMilliseconds(float durationMs)) |> ignore
                    act.Stop()
                    act.Dispose()
                | _ -> ()
            with _ -> ()
