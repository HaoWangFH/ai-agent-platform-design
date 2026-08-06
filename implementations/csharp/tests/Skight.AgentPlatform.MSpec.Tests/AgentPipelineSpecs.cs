using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Machine.Specifications;
using FluentAssertions;
using Azure.AI.OpenAI;
using Skight.AgentPlatform;

namespace Skight.AgentPlatform.MSpec.Tests
{
    [Subject("Agent Pipeline Core Loop - User Interrupt Branch")]
    public class When_user_requests_interrupt_during_turn_execution
    {
        Establish context = () =>
        {
            _registry = new ToolRegistry();
            _agent = new AgentRunner(new AgentConfig { ApiKey = "dummy_key", Model = "gpt-4o" }, _registry);
            _agent.RequestInterrupt();
        };

        Because of = () =>
            _result = _agent.RunAsync("Hello").GetAwaiter().GetResult();

        It should_set_interrupted_flag_to_true = () =>
            _result.Interrupted.Should().BeTrue();

        It should_set_exit_reason_to_interrupted = () =>
            _result.ExitReason.Should().Be("interrupted");

        static ToolRegistry _registry;
        static AgentRunner _agent;
        static TurnResult _result;
    }

    [Subject("Agent Pipeline Core Loop - Iteration Budget Exhaustion Branch")]
    public class When_turn_loop_exceeds_maximum_iteration_budget
    {
        Establish context = () =>
        {
            _registry = new ToolRegistry();
            _registry.Register("loop_tool", "Always loops", args => Task.FromResult("Looping..."), "{}");
            _agent = new AgentRunner(new AgentConfig { ApiKey = "dummy_key", Model = "gpt-4o", MaxIterations = 2 }, _registry);

            Func<List<FunctionDefinition>, List<ChatRequestMessage>, Task<ChatCompletions>> loopingLlmCaller =
                (schemas, msgs) => Task.FromResult(CreateToolCallCompletions("loop_call", "loop_tool", "{}"));

            _customLlm = loopingLlmCaller;
        };

        Because of = () =>
            _result = _agent.RunAsync("Keep looping", _customLlm).GetAwaiter().GetResult();

        It should_mark_turn_as_failed = () =>
            _result.Failed.Should().BeTrue();

        It should_set_exit_reason_to_budget_exhausted = () =>
            _result.ExitReason.Should().Be("budget_exhausted");

        It should_stop_exactly_at_max_iterations = () =>
            _result.ApiCalls.Should().Be(2);

        private static ChatCompletions CreateToolCallCompletions(string toolCallId, string toolName, string argumentsJson)
        {
            var toolCall = new ChatCompletionsFunctionToolCall(toolCallId, toolName, argumentsJson);
            var responseMsg = AzureOpenAIModelFactory.ChatResponseMessage(ChatRole.Assistant, "", new[] { toolCall });
            var choice = AzureOpenAIModelFactory.ChatChoice(message: responseMsg, index: 0, finishReason: CompletionsFinishReason.ToolCalls, logProbabilityInfo: null, contentFilterResults: null);
            return AzureOpenAIModelFactory.ChatCompletions(null, DateTimeOffset.UtcNow, new[] { choice }, null, null, null);
        }

        static ToolRegistry _registry;
        static AgentRunner _agent;
        static Func<List<FunctionDefinition>, List<ChatRequestMessage>, Task<ChatCompletions>> _customLlm;
        static TurnResult _result;
    }

    [Subject("Agent Pipeline Core Loop - Unregistered Tool Fallback Branch")]
    public class When_llm_invokes_unregistered_tool
    {
        Establish context = () =>
        {
            _registry = new ToolRegistry();
            _agent = new AgentRunner(new AgentConfig { ApiKey = "dummy_key", Model = "gpt-4o", MaxIterations = 2 }, _registry);

            int callCount = 0;
            _customLlm = (schemas, msgs) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    var toolCall = new ChatCompletionsFunctionToolCall("call_missing", "non_existent_tool", "{}");
                    var responseMsg = AzureOpenAIModelFactory.ChatResponseMessage(ChatRole.Assistant, "", new[] { toolCall });
                    var choice = AzureOpenAIModelFactory.ChatChoice(message: responseMsg, index: 0, finishReason: CompletionsFinishReason.ToolCalls, logProbabilityInfo: null, contentFilterResults: null);
                    return Task.FromResult(AzureOpenAIModelFactory.ChatCompletions(null, DateTimeOffset.UtcNow, new[] { choice }, null, null, null));
                }
                
                var finalText = AzureOpenAIModelFactory.ChatResponseMessage(ChatRole.Assistant, "Handled missing tool.", null);
                var textChoice = AzureOpenAIModelFactory.ChatChoice(message: finalText, index: 0, finishReason: CompletionsFinishReason.Stopped, logProbabilityInfo: null, contentFilterResults: null);
                return Task.FromResult(AzureOpenAIModelFactory.ChatCompletions(null, DateTimeOffset.UtcNow, new[] { textChoice }, null, null, null));
            };
        };

        Because of = () =>
            _result = _agent.RunAsync("Run missing tool", _customLlm).GetAwaiter().GetResult();

        It should_complete_turn_after_handling_missing_tool_message = () =>
            _result.Completed.Should().BeTrue();

        It should_append_unregistered_tool_error_message_to_history = () =>
            _result.Messages.Should().Contain(m => m is ChatRequestToolMessage && ((ChatRequestToolMessage)m).Content.Contains("is not registered"));

        static ToolRegistry _registry;
        static AgentRunner _agent;
        static Func<List<FunctionDefinition>, List<ChatRequestMessage>, Task<ChatCompletions>> _customLlm;
        static TurnResult _result;
    }

    [Subject("Agent Pipeline Core Loop - Malformed Tool JSON Argument Branch")]
    public class When_llm_passes_malformed_json_to_tool
    {
        Establish context = () =>
        {
            _registry = new ToolRegistry();
            _registry.Register("dummy_tool", "Dummy", args => Task.FromResult("OK"), "{}");
            _agent = new AgentRunner(new AgentConfig { ApiKey = "dummy_key", Model = "gpt-4o", MaxIterations = 2 }, _registry);

            int callCount = 0;
            _customLlm = (schemas, msgs) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    var toolCall = new ChatCompletionsFunctionToolCall("call_bad_json", "dummy_tool", "{ bad json ... ");
                    var responseMsg = AzureOpenAIModelFactory.ChatResponseMessage(ChatRole.Assistant, "", new[] { toolCall });
                    var choice = AzureOpenAIModelFactory.ChatChoice(message: responseMsg, index: 0, finishReason: CompletionsFinishReason.ToolCalls, logProbabilityInfo: null, contentFilterResults: null);
                    return Task.FromResult(AzureOpenAIModelFactory.ChatCompletions(null, DateTimeOffset.UtcNow, new[] { choice }, null, null, null));
                }

                var finalText = AzureOpenAIModelFactory.ChatResponseMessage(ChatRole.Assistant, "Recovered from JSON error.", null);
                var textChoice = AzureOpenAIModelFactory.ChatChoice(message: finalText, index: 0, finishReason: CompletionsFinishReason.Stopped, logProbabilityInfo: null, contentFilterResults: null);
                return Task.FromResult(AzureOpenAIModelFactory.ChatCompletions(null, DateTimeOffset.UtcNow, new[] { textChoice }, null, null, null));
            };
        };

        Because of = () =>
            _result = _agent.RunAsync("Pass bad JSON", _customLlm).GetAwaiter().GetResult();

        It should_append_json_parse_error_message_to_history = () =>
            _result.Messages.Should().Contain(m => m is ChatRequestToolMessage && ((ChatRequestToolMessage)m).Content.Contains("Invalid JSON arguments"));

        It should_complete_turn_successfully = () =>
            _result.Completed.Should().BeTrue();

        static ToolRegistry _registry;
        static AgentRunner _agent;
        static Func<List<FunctionDefinition>, List<ChatRequestMessage>, Task<ChatCompletions>> _customLlm;
        static TurnResult _result;
    }

    [Subject("Agent Pipeline Core Loop - Empty Response Recovery Branch")]
    public class When_llm_returns_empty_text_content
    {
        Establish context = () =>
        {
            _registry = new ToolRegistry();
            _agent = new AgentRunner(new AgentConfig { ApiKey = "dummy_key", Model = "gpt-4o", MaxIterations = 3 }, _registry);

            int callCount = 0;
            _customLlm = (schemas, msgs) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // Empty text response
                    var emptyMsg = AzureOpenAIModelFactory.ChatResponseMessage(ChatRole.Assistant, "", null);
                    var choice = AzureOpenAIModelFactory.ChatChoice(message: emptyMsg, index: 0, finishReason: CompletionsFinishReason.Stopped, logProbabilityInfo: null, contentFilterResults: null);
                    return Task.FromResult(AzureOpenAIModelFactory.ChatCompletions(null, DateTimeOffset.UtcNow, new[] { choice }, null, null, null));
                }

                var finalText = AzureOpenAIModelFactory.ChatResponseMessage(ChatRole.Assistant, "Nudged summary response.", null);
                var textChoice = AzureOpenAIModelFactory.ChatChoice(message: finalText, index: 0, finishReason: CompletionsFinishReason.Stopped, logProbabilityInfo: null, contentFilterResults: null);
                return Task.FromResult(AzureOpenAIModelFactory.ChatCompletions(null, DateTimeOffset.UtcNow, new[] { textChoice }, null, null, null));
            };
        };

        Because of = () =>
            _result = _agent.RunAsync("Tell me a secret", _customLlm).GetAwaiter().GetResult();

        It should_nudge_llm_with_user_prompt_recovery_message = () =>
            _result.Messages.Should().Contain(m => m is ChatRequestUserMessage && ((ChatRequestUserMessage)m).Content.Contains("Please provide a complete text response"));

        It should_eventually_complete_turn_with_recovered_text = () =>
            _result.FinalResponse.Should().Be("Nudged summary response.");

        static ToolRegistry _registry;
        static AgentRunner _agent;
        static Func<List<FunctionDefinition>, List<ChatRequestMessage>, Task<ChatCompletions>> _customLlm;
        static TurnResult _result;
    }
}
