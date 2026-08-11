using System;
using System.Collections.Generic;

namespace Skight.AgentPlatform
{
    public enum TelemetryEventType
    {
        SessionStart,
        TurnStart,
        ContextCompaction,
        LlmCall,
        ToolExecution,
        TurnEnd
    }

    public record TelemetryEvent
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString("N");
        public string TraceId { get; init; } = string.Empty;
        public string SpanId { get; init; } = Guid.NewGuid().ToString("N");
        public string? ParentSpanId { get; init; }
        public string SessionId { get; init; } = string.Empty;
        public string UserId { get; init; } = string.Empty;
        public int TurnIndex { get; init; }
        public TelemetryEventType EventType { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public long DurationMs { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Payload { get; init; } = string.Empty;
        public string RawPayload { get; init; } = string.Empty;
        public bool IsError { get; init; }
        public string? ExceptionDetails { get; init; }
        public Dictionary<string, object> Attributes { get; init; } = new();
    }
}
