using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Azure.AI.OpenAI;
using Skight.AgentPlatform;

namespace Skight.AgentPlatform.Tests
{
    public class AgentWorkflowTests
    {
        [Fact]
        public void TurnResult_InitializesDefaultValuesCorrectly()
        {
            var result = new TurnResult
            {
                FinalResponse = "Hello",
                ApiCalls = 1,
                Completed = true,
                ExitReason = "text_response"
            };

            result.FinalResponse.Should().Be("Hello");
            result.ApiCalls.Should().Be(1);
            result.Completed.Should().BeTrue();
            result.Failed.Should().BeFalse();
            result.Interrupted.Should().BeFalse();
            result.ExitReason.Should().Be("text_response");
        }

        [Fact]
        public async Task ToolRegistry_ExecuteToolAsync_ReturnsErrorForUnregisteredTool()
        {
            var registry = new ToolRegistry();
            
            var result = await registry.ExecuteToolAsync("unknown_tool", "{}");

            result.Should().Contain("Error: Tool 'unknown_tool' not found.");
        }

        [Fact]
        public async Task ToolRegistry_ExecuteToolAsync_ExecutesRegisteredToolSuccessfully()
        {
            var registry = new ToolRegistry();
            registry.Register("echo", "Echoes input", args => Task.FromResult($"Echo: {args}"), "{}");

            var result = await registry.ExecuteToolAsync("echo", "hello world");

            result.Should().Be("Echo: hello world");
        }

        [Fact]
        public async Task ToolRegistry_ExecuteToolAsync_CatchesToolRuntimeExceptions()
        {
            var registry = new ToolRegistry();
            registry.Register("failing_tool", "Fails always", args => throw new InvalidOperationException("Boom!"), "{}");

            var result = await registry.ExecuteToolAsync("failing_tool", "{}");

            result.Should().Contain("Error executing tool 'failing_tool': Boom!");
        }

        [Fact]
        public async Task Agent_InterruptRequested_StopsTurnExecution()
        {
            var registry = new ToolRegistry();
            var agent = new Agent("dummy_key", registry, "gpt-4o");
            
            agent.RequestInterrupt();
            var result = await agent.RunAsync("Hello");

            result.Interrupted.Should().BeTrue();
            result.ExitReason.Should().Be("interrupted");
        }
    }
}
