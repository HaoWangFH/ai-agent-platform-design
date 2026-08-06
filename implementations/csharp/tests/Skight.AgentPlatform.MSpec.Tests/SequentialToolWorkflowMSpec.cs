using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Machine.Specifications;
using FluentAssertions;
using Azure.AI.OpenAI;
using Skight.AgentPlatform;

namespace Skight.AgentPlatform.MSpec.Tests
{
    [Subject("Agent Conversation Loop - Multi-Turn Sequential Tool Execution")]
    public class When_user_requests_task_requiring_sequential_dependent_tools
    {
        Establish context = () =>
        {
            _registry = new ToolRegistry();
            _registry.Register("get_weather", "Gets weather", args => Task.FromResult("25°C, Sunny"), "{}");
            _registry.Register("search_contacts", "Searches contacts", args => Task.FromResult("alice@example.com"), "{}");

            _agent = new AgentRunner(new AgentConfig { ApiKey = "dummy_key", Model = "test-model" }, _registry);
            _callCounter = 0;

            _mockLlmCaller = (schemas, msgs) =>
            {
                _callCounter++;
                if (_callCounter == 1)
                    return Task.FromResult(CreateToolCallCompletions("call_weather_123", "get_weather", "{\"location\":\"Tokyo\"}"));
                if (_callCounter == 2)
                    return Task.FromResult(CreateToolCallCompletions("call_contact_456", "search_contacts", "{\"name\":\"Alice\"}"));
                if (_callCounter == 3)
                    return Task.FromResult(CreateTextCompletions("Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com)."));

                throw new InvalidOperationException("Unexpected LLM call");
            };
        };

        Because of = () =>
            _result = _agent.RunAsync("Find weather in Tokyo and notify Alice.", _mockLlmCaller).GetAwaiter().GetResult();

        It should_complete_the_turn_successfully = () =>
            _result.Completed.Should().BeTrue();

        It should_make_exactly_3_api_calls = () =>
            _result.ApiCalls.Should().Be(3);

        It should_store_all_7_messages_in_state = () =>
            _result.Messages.Should().HaveCount(7);

        It should_contain_the_final_text_response = () =>
            _result.FinalResponse.Should().Be("Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com).");

        private static ChatCompletions CreateToolCallCompletions(string toolCallId, string toolName, string argumentsJson)
        {
            var toolCall = new ChatCompletionsFunctionToolCall(toolCallId, toolName, argumentsJson);
            var responseMsg = AzureOpenAIModelFactory.ChatResponseMessage(ChatRole.Assistant, "", new[] { toolCall });
            var choice = AzureOpenAIModelFactory.ChatChoice(message: responseMsg, index: 0, finishReason: CompletionsFinishReason.ToolCalls, logProbabilityInfo: null, contentFilterResults: null);
            return AzureOpenAIModelFactory.ChatCompletions(null, DateTimeOffset.UtcNow, new[] { choice }, null, null, null);
        }

        private static ChatCompletions CreateTextCompletions(string textResponse)
        {
            var responseMsg = AzureOpenAIModelFactory.ChatResponseMessage(ChatRole.Assistant, textResponse, null);
            var choice = AzureOpenAIModelFactory.ChatChoice(message: responseMsg, index: 0, finishReason: CompletionsFinishReason.Stopped, logProbabilityInfo: null, contentFilterResults: null);
            return AzureOpenAIModelFactory.ChatCompletions(null, DateTimeOffset.UtcNow, new[] { choice }, null, null, null);
        }

        static ToolRegistry _registry;
        static AgentRunner _agent;
        static TurnResult _result;
        static int _callCounter;
        static Func<List<FunctionDefinition>, List<ChatRequestMessage>, Task<ChatCompletions>> _mockLlmCaller;
    }
}
