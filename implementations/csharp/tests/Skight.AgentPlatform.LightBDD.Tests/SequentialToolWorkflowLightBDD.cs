using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LightBDD.Framework;
using LightBDD.Framework.Scenarios;
using LightBDD.XUnit2;
using FluentAssertions;
using Azure.AI.OpenAI;
using Skight.AgentPlatform;

namespace Skight.AgentPlatform.LightBDD.Tests
{
    [FeatureDescription(@"In order to execute complex multi-step reasoning tasks
As an AI Agent
I want to execute dependent tools in sequence before generating the final answer")]
    public class SequentialToolWorkflowLightBDD : FeatureFixture
    {
        private ToolRegistry _registry = null!;
        private Agent _agent = null!;
        private TurnResult _result = null!;
        private int _callCounter;
        private Func<List<FunctionDefinition>, List<ChatRequestMessage>, Task<ChatCompletions>> _mockLlmCaller = null!;

        [Scenario]
        public async Task Multi_turn_sequential_dependent_tools_execution()
        {
            await Runner.RunScenarioAsync(
                _ => Given_registered_tools_for_weather_and_contacts(),
                _ => Given_mock_llm_configured_for_sequential_calls(),
                _ => When_agent_run_is_executed_with_prompt("Find weather in Tokyo and notify Alice."),
                _ => Then_turn_should_complete_successfully(),
                _ => Then_api_calls_should_equal(3),
                _ => Then_messages_count_should_equal(7),
                _ => Then_final_response_should_match_expected_text()
            );
        }

        private Task Given_registered_tools_for_weather_and_contacts()
        {
            _registry = new ToolRegistry();
            _registry.Register("get_weather", "Gets weather", args => Task.FromResult("25°C, Sunny"), "{}");
            _registry.Register("search_contacts", "Searches contacts", args => Task.FromResult("alice@example.com"), "{}");
            _agent = new Agent("dummy_key", _registry, "test-model");
            return Task.CompletedTask;
        }

        private Task Given_mock_llm_configured_for_sequential_calls()
        {
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
            return Task.CompletedTask;
        }

        private async Task When_agent_run_is_executed_with_prompt(string prompt)
        {
            _result = await _agent.RunAsync(prompt, _mockLlmCaller);
        }

        private Task Then_turn_should_complete_successfully()
        {
            _result.Completed.Should().BeTrue();
            _result.Failed.Should().BeFalse();
            return Task.CompletedTask;
        }

        private Task Then_api_calls_should_equal(int expected)
        {
            _result.ApiCalls.Should().Be(expected);
            return Task.CompletedTask;
        }

        private Task Then_messages_count_should_equal(int expected)
        {
            _result.Messages.Should().HaveCount(expected);
            return Task.CompletedTask;
        }

        private Task Then_final_response_should_match_expected_text()
        {
            _result.FinalResponse.Should().Be("Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com).");
            return Task.CompletedTask;
        }

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
    }
}
