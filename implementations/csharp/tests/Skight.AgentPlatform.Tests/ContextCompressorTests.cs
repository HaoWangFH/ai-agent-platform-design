using System.Collections.Generic;
using Azure.AI.OpenAI;
using Xunit;

namespace Skight.AgentPlatform.Tests
{
    public class ContextCompressorTests
    {
        [Fact]
        public void Compress_TriggersCompaction_WhenMessagesExceedThreshold()
        {
            var msgs = new List<ChatRequestMessage>
            {
                new ChatRequestSystemMessage("system prompt")
            };

            for (int i = 1; i <= 10; i++)
            {
                msgs.Add(new ChatRequestUserMessage($"user message {i}"));
            }

            // Limit = 10, threshold = 0.80 (8) -> 11 > 8, should compact
            var compressed = ContextCompressor.Compress(0.80, 10, msgs);

            Assert.True(compressed.Count < msgs.Count);
            Assert.Equal("system prompt", ((ChatRequestSystemMessage)compressed[0]).Content);
            var summaryMsg = compressed[1] as ChatRequestSystemMessage;
            Assert.NotNull(summaryMsg);
            Assert.Contains("[TURN SUMMARY]", summaryMsg.Content);
        }
    }
}
