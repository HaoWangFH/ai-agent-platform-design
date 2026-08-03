using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Azure.AI.OpenAI;
using AgentPlatform;

namespace AgentPlatform.Tests
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

            Assert.Equal("Hello", result.FinalResponse);
            Assert.Equal(1, result.ApiCalls);
            Assert.True(result.Completed);
            Assert.False(result.Failed);
            Assert.False(result.Interrupted);
            Assert.Equal("text_response", result.ExitReason);
        }

        [Fact]
        public async Task ToolRegistry_ExecuteToolAsync_ReturnsErrorForUnregisteredTool()
        {
            var registry = new ToolRegistry();
            
            var result = await registry.ExecuteToolAsync("unknown_tool", "{}");

            Assert.Contains("Error: Tool 'unknown_tool' not found.", result);
        }

        [Fact]
        public async Task ToolRegistry_ExecuteToolAsync_ExecutesRegisteredToolSuccessfully()
        {
            var registry = new ToolRegistry();
            registry.Register("echo", "Echoes input", args => Task.FromResult($"Echo: {args}"), "{}");

            var result = await registry.ExecuteToolAsync("echo", "hello world");

            Assert.Equal("Echo: hello world", result);
        }

        [Fact]
        public async Task ToolRegistry_ExecuteToolAsync_CatchesToolRuntimeExceptions()
        {
            var registry = new ToolRegistry();
            registry.Register("failing_tool", "Fails always", args => throw new InvalidOperationException("Boom!"), "{}");

            var result = await registry.ExecuteToolAsync("failing_tool", "{}");

            Assert.Contains("Error executing tool 'failing_tool': Boom!", result);
        }

        [Fact]
        public async Task Agent_InterruptRequested_StopsTurnExecution()
        {
            var registry = new ToolRegistry();
            var agent = new Agent("dummy_key", registry, "gpt-4o");
            
            agent.RequestInterrupt();
            var result = await agent.RunAsync("Hello");

            Assert.True(result.Interrupted);
            Assert.Equal("interrupted", result.ExitReason);
        }
    }
}
