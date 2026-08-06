using System;
using System.Threading.Tasks;
using Machine.Specifications;
using FluentAssertions;
using Skight.AgentPlatform;

namespace Skight.AgentPlatform.MSpec.Tests
{
    [Subject("Security & Approval Guard Module - Dangerous Command Interception")]
    public class When_agent_attempts_executing_destructive_system_command
    {
        Establish context = () =>
        {
            _dangerousCommand = "rm -rf /";
            _rejectPrompter = req => Task.FromResult(false);
        };

        Because of = () =>
        {
            _isHighRisk = ApprovalGuard.IsHighRiskCommand(_dangerousCommand);
            _exception = Catch.Exception(() => ApprovalGuard.EnforceCommandApprovalAsync(_dangerousCommand, _rejectPrompter).GetAwaiter().GetResult());
        };

        It should_classify_destructive_operation_as_high_risk = () =>
            _isHighRisk.Should().BeTrue();

        It should_reject_execution_with_unauthorized_access_exception = () =>
            _exception.Should().BeOfType<UnauthorizedAccessException>()
                .Which.Message.Should().Be("User rejected action.");

        static string _dangerousCommand;
        static Func<ApprovalRequest, Task<bool>> _rejectPrompter;
        static bool _isHighRisk;
        static Exception _exception;
    }

    [Subject("Security & Approval Guard Module - Safe Command Validation")]
    public class When_agent_executes_safe_non_destructive_command
    {
        Establish context = () =>
        {
            _safeCommand = "echo Hello World";
        };

        Because of = () =>
        {
            _isHighRisk = ApprovalGuard.IsHighRiskCommand(_safeCommand);
        };

        It should_not_flag_command_as_high_risk = () =>
            _isHighRisk.Should().BeFalse();

        static string _safeCommand;
        static bool _isHighRisk;
    }
}
