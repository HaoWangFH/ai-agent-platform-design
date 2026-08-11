using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Diagnostics;
using Azure.AI.OpenAI;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Skight.AgentPlatform
{
    public static class AgentTelemetry
    {
        public static TelemetryOptions Options { get; set; } = new TelemetryOptions();
        public static readonly ActivitySource ActivitySource = new("Skight.AgentPlatform", "1.0.0");
        private static TracerProvider? _tracerProvider;

        private static readonly Channel<TelemetryEvent> _channel = Channel.CreateUnbounded<TelemetryEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        private static readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private static readonly Task _processingTask;

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

                    var builder = Sdk.CreateTracerProviderBuilder()
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

            try
            {
                ActivityContext parentContext = default;
                if (!string.IsNullOrEmpty(evt.TraceId) && !string.IsNullOrEmpty(evt.ParentSpanId))
                {
                    try
                    {
                        var traceId = ActivityTraceId.CreateFromString(evt.TraceId.PadLeft(32, '0').AsSpan());
                        var parentSpanId = ActivitySpanId.CreateFromString(evt.ParentSpanId.PadLeft(16, '0').AsSpan());
                        parentContext = new ActivityContext(traceId, parentSpanId, ActivityTraceFlags.Recorded);
                    }
                    catch { }
                }

                var activity = parentContext != default
                    ? ActivitySource.StartActivity(evt.Name, ActivityKind.Internal, parentContext)
                    : ActivitySource.StartActivity(evt.Name, ActivityKind.Internal);

                if (activity != null)
                {
                    activity.SetTag("gen_ai.session.id", evt.SessionId);
                    activity.SetTag("gen_ai.user.id", evt.UserId);
                    activity.SetTag("gen_ai.turn.index", evt.TurnIndex);
                    activity.SetTag("event.type", evt.EventType.ToString());
                    activity.SetTag("payload", evt.Payload);

                    if (evt.IsError)
                    {
                        activity.SetStatus(ActivityStatusCode.Error, evt.Payload);
                        if (!string.IsNullOrEmpty(evt.ExceptionDetails))
                        {
                            activity.SetTag("exception.stacktrace", evt.ExceptionDetails);
                        }
                    }

                    foreach (var attr in evt.Attributes)
                    {
                        activity.SetTag(attr.Key, attr.Value);
                    }

                    if (evt.DurationMs > 0)
                    {
                        activity.SetEndTime(activity.StartTimeUtc.AddMilliseconds(evt.DurationMs));
                    }

                    activity.Stop();
                    activity.Dispose();
                }
            }
            catch { }
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
        }

        public static void TrackLlmCall(string sessionId, string userId, int turnIndex, string model, long durationMs, string responseContent, int toolCallsCount, string? traceId = null, string? parentSpanId = null)
        {
            TrackLlmCall(sessionId, userId, turnIndex, model, durationMs, responseContent, null as IReadOnlyList<ChatCompletionsToolCall>, traceId, parentSpanId);
        }

        public static void TrackLlmCall(string sessionId, string userId, int turnIndex, string model, long durationMs, string responseContent, IReadOnlyList<ChatCompletionsToolCall>? toolCalls, string? traceId = null, string? parentSpanId = null)
        {
            if (!Options.Enabled) return;
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

            string toolSummary = details.Count > 0 ? $" Requested ToolCalls: [{string.Join(", ", details)}]" : "";
            string payloadText = string.IsNullOrEmpty(responseContent)
                ? $"Model: {model}{toolSummary}"
                : $"Model: {model}, Content: {responseContent}{toolSummary}";

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
        }

        public static void TrackToolExecution(string sessionId, string userId, int turnIndex, string toolName, long durationMs, string argsJson, string result, string? traceId = null, string? parentSpanId = null, bool isError = false, Exception? exception = null)
        {
            if (!Options.Enabled) return;
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
                Payload = isError ? $"Tool '{toolName}' Error: {result}" : $"Args: {argsJson} => Result: {result}",
                RawPayload = $"Args: {argsJson}\nResult: {result}",
                IsError = isError,
                ExceptionDetails = exception?.ToString()
            });
        }

        public static void TrackTurnEnd(string sessionId, string userId, int turnIndex, long durationMs, string finalResponse, string exitReason, string? traceId = null, string? spanId = null)
        {
            if (!Options.Enabled) return;
            Track(new TelemetryEvent
            {
                TraceId = traceId ?? sessionId,
                SpanId = spanId ?? Guid.NewGuid().ToString("N"),
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
