using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Azure.AI.OpenAI;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;

namespace Skight.AgentPlatform
{
    public static class AgentTelemetry
    {
        public static TelemetryOptions Options { get; set; } = new TelemetryOptions();
        private static TracerProvider? _tracerProvider;
        private static readonly ActivitySource ActivitySource = new ActivitySource("Skight.AgentPlatform", "1.0.0");
        private static readonly Channel<TelemetryEvent> _channel = Channel.CreateUnbounded<TelemetryEvent>();
        private static readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private static readonly Task _processingTask;
        private static readonly ConcurrentDictionary<string, Activity> ActiveTurnActivities = new();

        public static async Task FlushAsync()
        {
            _channel.Writer.TryComplete();
            await _processingTask;
            _tracerProvider?.ForceFlush();
        }

        static AgentTelemetry()
        {
            InitializeOpenTelemetry();
            _processingTask = Task.Run(ProcessEventsAsync);
        }

        public static void InitializeOpenTelemetry()
        {
            if (!Options.Enabled || _tracerProvider != null) return;
            try
            {
                var endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                            ?? (string.IsNullOrWhiteSpace(Options.OtlpEndpoint) ? "http://localhost:4317" : Options.OtlpEndpoint);
                var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "csharp-agent-platform";

                var builder = Sdk.CreateTracerProviderBuilder()
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                    .AddSource("Skight.AgentPlatform")
                    .AddOtlpExporter(opt =>
                    {
                        opt.Endpoint = new Uri(endpoint);
                    });

                _tracerProvider = builder.Build();
            }
            catch
            {
                // Soft fallback if OTLP collector is not reachable locally
            }
        }

        public static void Track(TelemetryEvent evt)
        {
            if (!Options.Enabled) return;
            InitializeOpenTelemetry();
            _channel.Writer.TryWrite(evt);
        }

        public static void TrackSessionStart(string sessionId, string userId)
        {
            if (!Options.Enabled) return;
            Track(new TelemetryEvent
            {
                SessionId = sessionId,
                UserId = userId,
                EventType = TelemetryEventType.SessionStart,
                Name = "agent.session.start",
                Payload = $"Session started for user {userId}"
            });
        }

        public static void TrackTurnStart(string sessionId, string userId, int turnIndex, string userInput, string? traceId = null, string? spanId = null)
        {
            if (!Options.Enabled) return;
            InitializeOpenTelemetry();
            var tid = traceId ?? sessionId;
            var sid = spanId ?? Guid.NewGuid().ToString("N");
            Track(new TelemetryEvent
            {
                TraceId = tid,
                SpanId = sid,
                ParentSpanId = null,
                SessionId = sessionId,
                UserId = userId,
                TurnIndex = turnIndex,
                EventType = TelemetryEventType.TurnStart,
                Name = "agent.turn.start",
                Payload = userInput,
                RawPayload = userInput
            });

            try
            {
                var act = ActivitySource.StartActivity("agent.turn", ActivityKind.Internal);
                if (act != null)
                {
                    act.SetTag("gen_ai.session.id", sessionId);
                    act.SetTag("gen_ai.user.id", userId);
                    act.SetTag("gen_ai.turn.index", turnIndex);
                    act.SetTag("payload", userInput);
                    ActiveTurnActivities[sid] = act;
                }
            }
            catch { }
        }

        public static void TrackLlmCall(string sessionId, string userId, int turnIndex, string model, long durationMs, string responseContent, int toolCallsCount, string? traceId = null, string? parentSpanId = null)
        {
            TrackLlmCall(sessionId, userId, turnIndex, model, durationMs, responseContent, null as IReadOnlyList<ChatCompletionsToolCall>, traceId, parentSpanId);
        }

        public static void TrackLlmCall(string sessionId, string userId, int turnIndex, string model, long durationMs, string responseContent, IReadOnlyList<ChatCompletionsToolCall>? toolCalls, string? traceId = null, string? parentSpanId = null)
        {
            if (!Options.Enabled) return;
            InitializeOpenTelemetry();
            var details = new List<string>();
            if (toolCalls != null)
            {
                foreach (var tc in toolCalls)
                {
                    if (tc is ChatCompletionsFunctionToolCall ftc)
                    {
                        details.Add($"{ftc.Name}({ftc.Arguments})");
                    }
                }
            }
            var toolSummary = details.Count > 0 ? $" Requested ToolCalls: [{string.Join(", ", details)}]" : "";
            var payloadText = string.IsNullOrEmpty(responseContent) ? $"Model: {model}{toolSummary}" : $"Model: {model}, Content: {responseContent}{toolSummary}";

            Track(new TelemetryEvent
            {
                TraceId = traceId ?? sessionId,
                SpanId = Guid.NewGuid().ToString("N"),
                ParentSpanId = parentSpanId,
                SessionId = sessionId,
                UserId = userId,
                TurnIndex = turnIndex,
                EventType = TelemetryEventType.LlmCall,
                DurationMs = durationMs,
                Name = "llm.call",
                Payload = payloadText,
                RawPayload = $"Content: {responseContent}\nToolCalls:\n{string.Join("\n", details)}"
            });

            try
            {
                ActivityContext parentContext = default;
                if (!string.IsNullOrEmpty(parentSpanId) && ActiveTurnActivities.TryGetValue(parentSpanId, out var parentAct))
                {
                    parentContext = parentAct.Context;
                }

                var activity = parentContext != default
                    ? ActivitySource.StartActivity("llm.call", ActivityKind.Internal, parentContext)
                    : ActivitySource.StartActivity("llm.call", ActivityKind.Internal);

                if (activity != null)
                {
                    activity.SetTag("gen_ai.session.id", sessionId);
                    activity.SetTag("gen_ai.user.id", userId);
                    activity.SetTag("gen_ai.turn.index", turnIndex);
                    activity.SetTag("payload", payloadText);
                    if (durationMs > 0)
                    {
                        activity.SetEndTime(activity.StartTimeUtc.AddMilliseconds(durationMs));
                    }
                    activity.Stop();
                    activity.Dispose();
                }
            }
            catch { }
        }

        public static void TrackToolExecution(string sessionId, string userId, int turnIndex, string toolName, long durationMs, string argsJson, string result, string? traceId = null, string? parentSpanId = null, bool isError = false, Exception? exception = null)
        {
            if (!Options.Enabled) return;
            InitializeOpenTelemetry();
            var payloadText = isError ? $"Tool '{toolName}' Error: {result}" : $"Args: {argsJson} => Result: {result}";
            Track(new TelemetryEvent
            {
                TraceId = traceId ?? sessionId,
                SpanId = Guid.NewGuid().ToString("N"),
                ParentSpanId = parentSpanId,
                SessionId = sessionId,
                UserId = userId,
                TurnIndex = turnIndex,
                EventType = TelemetryEventType.ToolExecution,
                DurationMs = durationMs,
                Name = $"tool.execution:{toolName}",
                Payload = payloadText,
                RawPayload = $"Args: {argsJson}\nResult: {result}",
                IsError = isError,
                ExceptionDetails = exception?.ToString()
            });

            try
            {
                ActivityContext parentContext = default;
                if (!string.IsNullOrEmpty(parentSpanId) && ActiveTurnActivities.TryGetValue(parentSpanId, out var parentAct))
                {
                    parentContext = parentAct.Context;
                }

                var actName = $"tool.execution:{toolName}";
                var activity = parentContext != default
                    ? ActivitySource.StartActivity(actName, ActivityKind.Internal, parentContext)
                    : ActivitySource.StartActivity(actName, ActivityKind.Internal);

                if (activity != null)
                {
                    activity.SetTag("gen_ai.session.id", sessionId);
                    activity.SetTag("gen_ai.user.id", userId);
                    activity.SetTag("gen_ai.turn.index", turnIndex);
                    activity.SetTag("payload", payloadText);
                    if (isError)
                    {
                        activity.SetStatus(ActivityStatusCode.Error, payloadText);
                    }
                    if (durationMs > 0)
                    {
                        activity.SetEndTime(activity.StartTimeUtc.AddMilliseconds(durationMs));
                    }
                    activity.Stop();
                    activity.Dispose();
                }
            }
            catch { }
        }

        public static void TrackTurnEnd(string sessionId, string userId, int turnIndex, long durationMs, string finalResponse, string exitReason, string? traceId = null, string? spanId = null)
        {
            if (!Options.Enabled) return;
            InitializeOpenTelemetry();
            var tid = traceId ?? sessionId;
            var sid = spanId ?? Guid.NewGuid().ToString("N");
            Track(new TelemetryEvent
            {
                TraceId = tid,
                SpanId = sid,
                ParentSpanId = null,
                SessionId = sessionId,
                UserId = userId,
                TurnIndex = turnIndex,
                EventType = TelemetryEventType.TurnEnd,
                DurationMs = durationMs,
                Name = "agent.turn.end",
                Payload = $"ExitReason: {exitReason}, ResponseLength: {finalResponse.Length}",
                RawPayload = finalResponse
            });

            try
            {
                if (ActiveTurnActivities.TryRemove(sid, out var act) && act != null)
                {
                    act.SetTag("payload", $"ExitReason: {exitReason}, ResponseLength: {finalResponse.Length}");
                    if (exitReason != "completed" && exitReason != "text_response")
                    {
                        act.SetStatus(ActivityStatusCode.Error, $"ExitReason: {exitReason}");
                    }
                    if (durationMs > 0)
                    {
                        act.SetEndTime(act.StartTimeUtc.AddMilliseconds(durationMs));
                    }
                    act.Stop();
                    act.Dispose();
                }
            }
            catch { }
        }

        private static async Task ProcessEventsAsync()
        {
            var reader = _channel.Reader;
            while (await reader.WaitToReadAsync(_cts.Token))
            {
                while (reader.TryRead(out var evt))
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(evt.SessionId)) continue;
                        
                        var dir = Path.Combine(Options.LogDirectory, evt.SessionId);
                        Directory.CreateDirectory(dir);

                        var compactPath = Path.Combine(dir, "transcript.jsonl");
                        var fullPath = Path.Combine(dir, "transcript_full.jsonl");

                        var jsonOptions = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                        var compactJson = JsonSerializer.Serialize(new
                        {
                            evt.EventId,
                            evt.TraceId,
                            evt.SpanId,
                            evt.ParentSpanId,
                            evt.SessionId,
                            evt.UserId,
                            evt.TurnIndex,
                            EventType = evt.EventType.ToString(),
                            evt.Timestamp,
                            evt.DurationMs,
                            evt.Name,
                            evt.Payload,
                            evt.IsError,
                            evt.ExceptionDetails
                        }, jsonOptions);

                        var fullJson = JsonSerializer.Serialize(evt, jsonOptions);

                        await File.AppendAllTextAsync(compactPath, compactJson + "\n", _cts.Token);
                        await File.AppendAllTextAsync(fullPath, fullJson + "\n", _cts.Token);
                    }
                    catch
                    {
                        // Background telemetry failures never crash agent turns
                    }
                }
            }
        }
    }
}
