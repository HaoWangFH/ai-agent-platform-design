using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Azure.AI.OpenAI;
using Skight.AgentPlatform;

namespace Skight.AgentPlatform.Tests
{
    /// <summary>
    /// Specification-Driven Test Suite (xUnit + FluentAssertions): Multi-Turn Sequential Tool Execution Loop
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
            result.Completed.Should().BeTrue("Turn should complete successfully");
            result.Failed.Should().BeFalse("Turn should not fail");
            result.ApiCalls.Should().Be(3);
            result.FinalResponse.Should().Be("Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com).");

            // Assert exact 7 messages in state:
            // [0] System, [1] User, [2] Asst(Tool1), [3] Tool(Res1), [4] Asst(Tool2), [5] Tool(Res2), [6] Asst(FinalText)
            result.Messages.Should().HaveCount(7);

            // Verify Message 2 & 3 (First tool cycle)
            var asstMsg1 = result.Messages[2].Should().BeOfType<ChatRequestAssistantMessage>().Subject;
            asstMsg1.ToolCalls.Should().NotBeNull();
            var fnCall1 = asstMsg1.ToolCalls[0].Should().BeOfType<ChatCompletionsFunctionToolCall>().Subject;
            fnCall1.Name.Should().Be("get_weather");

            var toolMsg1 = result.Messages[3].Should().BeOfType<ChatRequestToolMessage>().Subject;
            toolMsg1.Content.Should().Be("25°C, Sunny");
            toolMsg1.ToolCallId.Should().Be("call_weather_123");

            // Verify Message 4 & 5 (Second tool cycle)
            var asstMsg2 = result.Messages[4].Should().BeOfType<ChatRequestAssistantMessage>().Subject;
            asstMsg2.ToolCalls.Should().NotBeNull();
            var fnCall2 = asstMsg2.ToolCalls[0].Should().BeOfType<ChatCompletionsFunctionToolCall>().Subject;
            fnCall2.Name.Should().Be("search_contacts");

            var toolMsg2 = result.Messages[5].Should().BeOfType<ChatRequestToolMessage>().Subject;
            toolMsg2.Content.Should().Be("alice@example.com");
            toolMsg2.ToolCallId.Should().Be("call_contact_456");

            // Verify Message 6 (Final text response)
            var asstMsg3 = result.Messages[6].Should().BeOfType<ChatRequestAssistantMessage>().Subject;
            asstMsg3.Content.Should().Be("Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com).");
        }
    }
}
