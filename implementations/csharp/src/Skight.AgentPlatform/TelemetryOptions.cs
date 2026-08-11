using System;

namespace Skight.AgentPlatform
{
    public enum TelemetryStorageType
    {
        JsonL,
        OpenTelemetry,
        Dual
    }

    public class TelemetryOptions
    {
        public bool Enabled { get; set; } = true;
        public TelemetryStorageType StorageType { get; set; } = TelemetryStorageType.Dual;
        public string LogDirectory { get; set; } = "logs/transcripts";
        public string OtlpEndpoint { get; set; } = "http://localhost:4317";
        public string ConnectionString { get; set; } = string.Empty;
        public int BatchSize { get; set; } = 50;
        public int FlushIntervalMs { get; set; } = 500;
    }
}
