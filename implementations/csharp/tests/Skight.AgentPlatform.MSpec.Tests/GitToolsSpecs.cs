using System;
using System.IO;
using System.Text.Json;
using Machine.Specifications;
using FluentAssertions;
using Skight.AgentPlatform;

namespace Skight.AgentPlatform.MSpec.Tests
{
    [Subject("Git Automation Module - Status, Staging and Committing")]
    public class When_developer_requests_git_commit_on_untracked_files
    {
        Establish context = () =>
        {
            _repoDir = Path.Combine(Path.GetTempPath(), "test_git_repo_mspec_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_repoDir);

            TerminalTool.ExecuteCommandAsync($"git -C \"{_repoDir}\" init").GetAwaiter().GetResult();
            TerminalTool.ExecuteCommandAsync($"git -C \"{_repoDir}\" config user.name \"TestUser\"").GetAwaiter().GetResult();
            TerminalTool.ExecuteCommandAsync($"git -C \"{_repoDir}\" config user.email \"test@example.com\"").GetAwaiter().GetResult();

            var filePath = Path.Combine(_repoDir, "feature.txt");
            File.WriteAllText(filePath, "New feature implementation code");
        };

        Because of = () =>
        {
            _statusOutput = GitTools.GitStatusAsync(_repoDir).GetAwaiter().GetResult();
            var commitArgs = JsonSerializer.Serialize(new { message = "Implement new feature" });
            _commitOutput = GitTools.GitCommitAsync(_repoDir, commitArgs).GetAwaiter().GetResult();
            _logOutput = TerminalTool.ExecuteCommandAsync($"git -C \"{_repoDir}\" log -1").GetAwaiter().GetResult();
        };

        It should_report_untracked_files_in_git_status = () =>
            _statusOutput.Should().Contain("feature.txt");

        It should_successfully_commit_changes = () =>
            _commitOutput.Should().NotContain("Error");

        It should_record_the_commit_message_in_git_log = () =>
            _logOutput.Should().Contain("Implement new feature");

        Cleanup after = () =>
        {
            if (Directory.Exists(_repoDir))
            {
                try { Directory.Delete(_repoDir, true); } catch { }
            }
        };

        static string _repoDir;
        static string _statusOutput;
        static string _commitOutput;
        static string _logOutput;
    }
}
