using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Xunit;

namespace Skight.AgentPlatform.Tests
{
    public class SteeringTests
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
        public async Task PreApiSteeringDrain_InjectsMidTurnMessage_IntoToolMessageContent()
        {
            var config = new AgentConfig { Model = "gpt-4o", MaxIterations = 5 };
            var registry = new ToolRegistry();
            AgentRunner? runner = null;

            registry.Register("read_file", "read file", args =>
            {
                runner?.EnqueueSteering("Focus on HTTPS configuration instead of HTTP");
                return Task.FromResult("port=8080");
            }, "{}");

            runner = new AgentRunner(config, registry);
            int callCount = 0;
            List<ChatRequestMessage>? secondCallMessages = null;

            var result = await runner.RunAsync("Start server", (tools, msgs) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return Task.FromResult(CreateToolCallCompletions("call_1", "read_file", "{}"));
                }
                else
                {
                    secondCallMessages = msgs;
                    return Task.FromResult(CreateTextCompletions("Steered server setup complete."));
                }
            });

            Assert.Equal(2, result.ApiCalls);
            Assert.NotNull(secondCallMessages);
            var lastMsg = secondCallMessages.Last() as ChatRequestToolMessage;
            Assert.NotNull(lastMsg);
            Assert.Contains("[USER STEERING INTERRUPT]: Focus on HTTPS configuration instead of HTTP", lastMsg.Content);
        }
    }
}
