using System.Threading.Tasks;
using Xunit;

namespace Skight.AgentPlatform.Tests
{
    public class DelegateToolTests
    {
        [Fact]
        public async Task DelegateTaskAsync_SingleTask_ParsesAndExecutes()
        {
            var config = new AgentConfig { Model = "gpt-4o" };
            var registry = new ToolRegistry();

            var jsonArgs = "{\"role\":\"inspector\",\"task\":\"Analyze logs\"}";
            var result = await DelegateTool.DelegateTaskAsync(config, registry, jsonArgs);

            Assert.Contains("Subagent (inspector) output:", result);
        }

        [Fact]
        public async Task DelegateTaskAsync_BatchParallelTasks_ExecutesAllConcurrently()
        {
            var config = new AgentConfig { Model = "gpt-4o" };
            var registry = new ToolRegistry();

            var jsonArgs = @"
            {
                ""tasks"": [
                    {""role"": ""worker_1"", ""task"": ""Task A""},
                    {""role"": ""worker_2"", ""task"": ""Task B""}
                ]
            }";

            var result = await DelegateTool.DelegateTaskAsync(config, registry, jsonArgs);

            Assert.Contains("Batch Subagent Execution Results:", result);
            Assert.Contains("Subagent (worker_1) output:", result);
            Assert.Contains("Subagent (worker_2) output:", result);
        }
    }
}
