using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public static class AgentTelemetry
    {
        public static TelemetryOptions Options { get; set; } = new TelemetryOptions();

        private static readonly Channel<TelemetryEvent> _channel = Channel.CreateUnbounded<TelemetryEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        private static readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private static readonly Task _processingTask;

        static AgentTelemetry()
        {
            _processingTask = Task.Run(ProcessEventsAsync);
        }

        public static void Track(TelemetryEvent evt)
        {
            if (!Options.Enabled) return;
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
            if (!Options.Enabled) return;
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
                Payload = $"Model: {model}, ResponseLength: {responseContent.Length}, ToolCalls: {toolCallsCount}",
                RawPayload = responseContent
            });
        }

        public static void TrackToolExecution(string sessionId, string userId, int turnIndex, string toolName, long durationMs, string argsJson, string result, string? traceId = null, string? parentSpanId = null)
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
                Payload = $"Tool: {toolName}, ResultLength: {result.Length}",
                RawPayload = $"Args: {argsJson}\nResult: {result}"
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
                            evt.Payload
                        });

                        var fullJson = JsonSerializer.Serialize(evt);

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
