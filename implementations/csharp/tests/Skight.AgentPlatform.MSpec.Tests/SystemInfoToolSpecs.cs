using System;
using System.IO;
using Machine.Specifications;
using FluentAssertions;
using Skight.AgentPlatform;

namespace Skight.AgentPlatform.MSpec.Tests
{
    [Subject("System Info Tool Module - Environment Metadata Inspection")]
    public class When_agent_queries_system_info
    {
        Establish context = () =>
        {
            _workspace = Directory.GetCurrentDirectory();
        };

        Because of = () =>
        {
            _jsonResult = SystemInfoTool.GetSystemInfoAsync(_workspace).GetAwaiter().GetResult();
        };

        It should_return_json_containing_os_description = () =>
            _jsonResult.Should().Contain("os");

        It should_contain_machine_and_workspace_information = () =>
            _jsonResult.Should().Contain("workspace");

        static string _workspace;
        static string _jsonResult;
    }
}
