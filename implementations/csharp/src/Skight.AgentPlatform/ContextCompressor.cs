using System;
using System.Collections.Generic;
using System.Linq;
using Azure.AI.OpenAI;

namespace Skight.AgentPlatform
{
    public static class ContextCompressor
    {
        public static List<ChatRequestMessage> Compress(double thresholdRatio, int limit, List<ChatRequestMessage> messages)
        {
            if (messages == null || messages.Count == 0) return new List<ChatRequestMessage>();

            int triggerThreshold = (int)(limit * thresholdRatio);
            if (messages.Count <= triggerThreshold)
            {
                return messages;
            }

            Console.WriteLine($"  [Context Compaction Engine] History size ({messages.Count}) exceeds threshold ({triggerThreshold} of limit {limit}). Compacting...");

            var systemPrompt = messages[0];
            int keepRecentCount = Math.Max(3, limit / 3);

            var recentMessages = messages
                .Skip(Math.Max(0, messages.Count - keepRecentCount))
                .SkipWhile(m => m is ChatRequestToolMessage)
                .ToList();

            int trimmedCount = messages.Count - recentMessages.Count - 1;
            string summaryContent = $"[TURN SUMMARY]: {trimmedCount} past conversation turns were compacted to maintain token budget. Key focus is retained in recent context.";

            var summaryMsg = new ChatRequestSystemMessage(summaryContent);

            var result = new List<ChatRequestMessage> { systemPrompt, summaryMsg };
            result.AddRange(recentMessages);
            return result;
        }
    }
}
