using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace Skight.AgentPlatform.Tests
{
    public class TelemetryTests
    {
        [Fact]
        public async Task Telemetry_WhenEnabled_WritesJsonlLogsAsync()
        {
            var testLogDir = Path.Combine(Path.GetTempPath(), "agent_telemetry_test_" + Guid.NewGuid().ToString("N"));
            AgentTelemetry.Options = new TelemetryOptions
            {
                Enabled = true,
                LogDirectory = testLogDir
            };

            var sessionId = "test_session_" + Guid.NewGuid().ToString("N");
            AgentTelemetry.TrackTurnStart(sessionId, "dev_user", 1, "List files in current directory");
            AgentTelemetry.TrackToolExecution(sessionId, "dev_user", 1, "file_list", 15L, "{}", "[\"file1.txt\"]");
            AgentTelemetry.TrackTurnEnd(sessionId, "dev_user", 1, 100L, "Found file1.txt", "completed");

            await Task.Delay(300); // Allow background channel worker to flush

            var sessionDir = Path.Combine(testLogDir, sessionId);
            var compactPath = Path.Combine(sessionDir, "transcript.jsonl");
            var fullPath = Path.Combine(sessionDir, "transcript_full.jsonl");

            File.Exists(compactPath).Should().BeTrue();
            File.Exists(fullPath).Should().BeTrue();

            var compactLines = await File.ReadAllLinesAsync(compactPath);
            compactLines.Length.Should().Be(3);
            compactLines[0].Should().Contain("agent.turn.start");
            compactLines[1].Should().Contain("tool.execution:file_list");
            compactLines[2].Should().Contain("agent.turn.end");

            // Cleanup
            Directory.Delete(testLogDir, true);
        }

        [Fact]
        public async Task Telemetry_WhenDisabled_ProducesZeroFileLogs()
        {
            var testLogDir = Path.Combine(Path.GetTempPath(), "agent_telemetry_test_disabled_" + Guid.NewGuid().ToString("N"));
            AgentTelemetry.Options = new TelemetryOptions
            {
                Enabled = false,
                LogDirectory = testLogDir
            };

            var sessionId = "disabled_session_" + Guid.NewGuid().ToString("N");
            AgentTelemetry.TrackTurnStart(sessionId, "dev_user", 1, "Disabled test");

            await Task.Delay(100);

            var sessionDir = Path.Combine(testLogDir, sessionId);
            Directory.Exists(sessionDir).Should().BeFalse();
        }
    }
}
