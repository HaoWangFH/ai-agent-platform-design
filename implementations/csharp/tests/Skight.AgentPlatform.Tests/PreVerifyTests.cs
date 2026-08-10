using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Xunit;

namespace Skight.AgentPlatform.Tests
{
    public class PreVerifyTests
    {
        private ChatCompletions CreateToolCallCompletions(string toolCallId, string toolName, string argumentsJson)
        {
            var toolCall = new ChatCompletionsFunctionToolCall(toolCallId, toolName, argumentsJson);
            var responseMsg = AzureOpenAIModelFactory.ChatResponseMessage(ChatRole.Assistant, "", new[] { toolCall });
            var choice = AzureOpenAIModelFactory.ChatChoice(message: responseMsg, index: 0, finishReason: CompletionsFinishReason.ToolCalls, logProbabilityInfo: null, contentFilterResults: null);
            return AzureOpenAIModelFactory.ChatCompletions(null, DateTimeOffset.UtcNow, new[] { choice }, null, null, null);
        }

        private ChatCompletions CreateTextCompletions(string textResponse)
        {
            var responseMsg = AzureOpenAIModelFactory.ChatResponseMessage(ChatRole.Assistant, textResponse, null);
            var choice = AzureOpenAIModelFactory.ChatChoice(message: responseMsg, index: 0, finishReason: CompletionsFinishReason.Stopped, logProbabilityInfo: null, contentFilterResults: null);
            return AzureOpenAIModelFactory.ChatCompletions(null, DateTimeOffset.UtcNow, new[] { choice }, null, null, null);
        }

        [Fact]
        public async Task PreVerifyGate_InterceptsTurn_WhenFilesModifiedWithoutVerification()
        {
            var config = new AgentConfig { Model = "gpt-4o", MaxIterations = 10 };
            var registry = new ToolRegistry();
            registry.Register("write_to_file", "write file", args => Task.FromResult("file written"), "{}");

            var runner = new AgentRunner(config, registry);
            int callCount = 0;

            var result = await runner.RunAsync("Fix bug", (tools, msgs) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return Task.FromResult(CreateToolCallCompletions("call_1", "write_to_file", "{}"));
                }
                else
                {
                    return Task.FromResult(CreateTextCompletions("Completed all changes."));
                }
            });

            Assert.Equal(4, result.ApiCalls);
            Assert.True(result.Completed);
            Assert.Equal("Completed all changes.", result.FinalResponse);
        }
    }
}
