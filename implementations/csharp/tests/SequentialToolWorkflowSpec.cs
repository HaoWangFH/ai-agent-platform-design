using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Azure.AI.OpenAI;
using AgentPlatform;

namespace AgentPlatform.Tests
{
    /// <summary>
    /// Specification-Driven Test Suite: Multi-Turn Sequential Tool Execution Loop
    /// 
    /// SPECIFICATION:
    /// Given a user request requiring multiple sequential tools ("get_weather" -> "search_contacts")
    /// And a mock LLM configured to return dependent tool calls across turns
    /// When the turn loop runs
    /// Then the agent must iteratively execute tools, update state, and return the final text response.
    /// </summary>
    public class SequentialToolWorkflowSpec
    {
        private ChatCompletions CreateToolCallCompletions(string toolCallId, string toolName, string argumentsJson)
        {
            var toolCall = new ChatCompletionsFunctionToolCall(toolCallId, toolName, argumentsJson);
            var responseMsg = AzureOpenAIModelFactory.ChatResponseMessage(ChatRole.Assistant, "", new[] { toolCall });
            var choice = AzureOpenAIModelFactory.ChatChoice(responseMsg, 0, CompletionsFinishReason.ToolCalls, null);
            return AzureOpenAIModelFactory.ChatCompletions(null, DateTimeOffset.UtcNow, new[] { choice }, null, null, null);
        }

        private ChatCompletions CreateTextCompletions(string textResponse)
        {
            var responseMsg = AzureOpenAIModelFactory.ChatResponseMessage(ChatRole.Assistant, textResponse, null);
            var choice = AzureOpenAIModelFactory.ChatChoice(responseMsg, 0, CompletionsFinishReason.Stopped, null);
            return AzureOpenAIModelFactory.ChatCompletions(null, DateTimeOffset.UtcNow, new[] { choice }, null, null, null);
        }

        [Fact]
        public async Task SPEC_MultiTurnSequentialToolCallWorkflow_ExecutesDependentToolsAndFinalizesResponse()
        {
            // =========================================================================================
            // GIVEN: A registered ToolRegistry with get_weather and search_contacts
            // =========================================================================================
            var registry = new ToolRegistry();
            registry.Register("get_weather", "Gets weather", args => Task.FromResult("25°C, Sunny"), "{}");
            registry.Register("search_contacts", "Searches contacts", args => Task.FromResult("alice@example.com"), "{}");

            var agent = new Agent("dummy_key", registry, "test-model");

            // GIVEN: A mock LLM configured for 3 sequential calls
            int callCounter = 0;
            Func<List<FunctionDefinition>, List<ChatRequestMessage>, Task<ChatCompletions>> mockLlmCaller =
                (schemas, msgs) =>
                {
                    callCounter++;
                    if (callCounter == 1)
                        return Task.FromResult(CreateToolCallCompletions("call_weather_123", "get_weather", "{\"location\":\"Tokyo\"}"));
                    if (callCounter == 2)
                        return Task.FromResult(CreateToolCallCompletions("call_contact_456", "search_contacts", "{\"name\":\"Alice\"}"));
                    if (callCounter == 3)
                        return Task.FromResult(CreateTextCompletions("Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com)."));
                    
                    throw new InvalidOperationException("Unexpected LLM call beyond sequence");
                };

            // =========================================================================================
            // WHEN: Executing the turn loop with user prompt
            // =========================================================================================
            var result = await agent.RunAsync("Find weather in Tokyo and notify Alice.", mockLlmCaller);

            // =========================================================================================
            // THEN: Verify turn completion and full message history sequence
            // =========================================================================================
            Assert.True(result.Completed, "Turn should complete successfully");
            Assert.False(result.Failed, "Turn should not fail");
            Assert.Equal(3, result.ApiCalls);
            Assert.Equal("Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com).", result.FinalResponse);

            // Assert exact 7 messages in state:
            // [0] System, [1] User, [2] Asst(Tool1), [3] Tool(Res1), [4] Asst(Tool2), [5] Tool(Res2), [6] Asst(FinalText)
            Assert.Equal(7, result.Messages.Count);

            // Verify Message 2 & 3 (First tool cycle)
            var asstMsg1 = Assert.IsType<ChatRequestAssistantMessage>(result.Messages[2]);
            Assert.NotNull(asstMsg1.ToolCalls);
            var fnCall1 = Assert.IsType<ChatCompletionsFunctionToolCall>(asstMsg1.ToolCalls[0]);
            Assert.Equal("get_weather", fnCall1.Name);

            var toolMsg1 = Assert.IsType<ChatRequestToolMessage>(result.Messages[3]);
            Assert.Equal("25°C, Sunny", toolMsg1.Content);
            Assert.Equal("call_weather_123", toolMsg1.ToolCallId);

            // Verify Message 4 & 5 (Second tool cycle)
            var asstMsg2 = Assert.IsType<ChatRequestAssistantMessage>(result.Messages[4]);
            Assert.NotNull(asstMsg2.ToolCalls);
            var fnCall2 = Assert.IsType<ChatCompletionsFunctionToolCall>(asstMsg2.ToolCalls[0]);
            Assert.Equal("search_contacts", fnCall2.Name);

            var toolMsg2 = Assert.IsType<ChatRequestToolMessage>(result.Messages[5]);
            Assert.Equal("alice@example.com", toolMsg2.Content);
            Assert.Equal("call_contact_456", toolMsg2.ToolCallId);

            // Verify Message 6 (Final text response)
            var asstMsg3 = Assert.IsType<ChatRequestAssistantMessage>(result.Messages[6]);
            Assert.Equal("Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com).", asstMsg3.Content);
        }
    }
}
