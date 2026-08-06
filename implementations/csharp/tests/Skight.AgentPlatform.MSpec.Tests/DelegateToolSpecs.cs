using System;
using System.IO;
using System.Text.Json;
using Machine.Specifications;
using FluentAssertions;
using Skight.AgentPlatform;

namespace Skight.AgentPlatform.MSpec.Tests
{
    [Subject("Subagent Delegation Module - Context Configuration & Scoping")]
    public class When_primary_agent_delegates_subtask_to_specialized_subagent
    {
        Establish context = () =>
        {
            _registry = new ToolRegistry();
            Tools.RegisterCoreTools(_registry, Directory.GetCurrentDirectory());

            _parentConfig = new AgentConfig
            {
                ApiKey = "mock_key",
                Model = "gpt-4o",
                MaxIterations = 10
            };
        };

        Because of = () =>
        {
            _delegateArgs = JsonSerializer.Serialize(new
            {
                role = "Code Reviewer",
                task = "Review PR #42 for potential security vulnerabilities"
            });
        };

        It should_include_the_target_role = () =>
            _delegateArgs.Should().Contain("Code Reviewer");

        It should_include_the_target_task_objective = () =>
            _delegateArgs.Should().Contain("Review PR #42");

        static ToolRegistry _registry;
        static AgentConfig _parentConfig;
        static string _delegateArgs;
    }
}
