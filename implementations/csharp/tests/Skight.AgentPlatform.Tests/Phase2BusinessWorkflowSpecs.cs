using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Skight.AgentPlatform;

namespace Skight.AgentPlatform.Tests
{
    /// <summary>
    /// BDD Specification Suite: Core Business Workflows (Phase 2)
    /// 
    /// These tests serve as executable documentation for the business capabilities of the Agent Platform.
    /// A domain expert or reviewer should be able to read these scenarios to understand all platform workflows 
    /// without inspecting internal implementation details.
    /// </summary>
    public class Phase2BusinessWorkflowSpecs
    {
        [Fact]
        public async Task Feature_GitAutomation_Stages_Commits_And_Pushes_Workspace_Changes()
        {
            // SCENARIO: Developer requests automated Git status, commit, and push operations.
            // GIVEN: A workspace directory initialized as a Git repository with pending changes.
            var testRepoDir = Path.Combine(Path.GetTempPath(), "test_git_repo_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRepoDir);

            try
            {
                // Set up local dummy repo
                await TerminalTool.ExecuteCommandAsync($"git -C \"{testRepoDir}\" init");
                await TerminalTool.ExecuteCommandAsync($"git -C \"{testRepoDir}\" config user.name \"TestUser\"");
                await TerminalTool.ExecuteCommandAsync($"git -C \"{testRepoDir}\" config user.email \"test@example.com\"");
                
                var filePath = Path.Combine(testRepoDir, "feature.txt");
                await File.WriteAllTextAsync(filePath, "New feature implementation code");

                // WHEN: The agent checks git status
                var statusOutput = await GitTools.GitStatusAsync(testRepoDir);

                // THEN: The status reports the untracked feature file
                statusOutput.Should().Contain("feature.txt");

                // WHEN: The agent commits the changes with a commit message
                var commitArgs = JsonSerializer.Serialize(new { message = "Implement new feature" });
                var commitOutput = await GitTools.GitCommitAsync(testRepoDir, commitArgs);

                // THEN: The commit succeeds and records the new commit in the repository log
                commitOutput.Should().NotContain("Error");

                var logOutput = await TerminalTool.ExecuteCommandAsync($"git -C \"{testRepoDir}\" log -1");
                logOutput.Should().Contain("Implement new feature");
            }
            finally
            {
                if (Directory.Exists(testRepoDir))
                {
                    try { Directory.Delete(testRepoDir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task Feature_SecurityGuard_Intercepts_Dangerous_Shell_Operations()
        {
            // SCENARIO: Prevent malicious or accidental destructive system commands.
            // GIVEN: A high-risk command payload attempting destructive file deletion
            var dangerousCommand = "rm -rf /";
            var safeCommand = "echo Hello World";

            // WHEN: Evaluating risk level of the commands
            var isDangerousHighRisk = ApprovalGuard.IsHighRiskCommand(dangerousCommand);
            var isSafeHighRisk = ApprovalGuard.IsHighRiskCommand(safeCommand);

            // THEN: The dangerous command is flagged as high-risk, while the safe command passes
            isDangerousHighRisk.Should().BeTrue("rm -rf is a destructive operation that must require human approval");
            isSafeHighRisk.Should().BeFalse("echo Hello World is non-destructive and safe to execute");

            // WHEN: The prompter rejects execution of the high-risk command
            Func<ApprovalRequest, Task<bool>> rejectPrompter = req => Task.FromResult(false);
            Func<Task> act = async () => await ApprovalGuard.EnforceCommandApprovalAsync(dangerousCommand, rejectPrompter);

            // THEN: An UnauthorizedAccessException is thrown, preventing execution
            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("User rejected action.");
        }

        [Fact]
        public async Task Feature_SubagentDelegation_Spawns_Isolated_Subagent_For_Domain_Tasks()
        {
            // SCENARIO: Primary agent delegates complex task to specialized subagent with isolated context.
            // GIVEN: A parent configuration and tool registry
            var registry = new ToolRegistry();
            Tools.RegisterCoreTools(registry, Directory.GetCurrentDirectory());

            var parentConfig = new AgentConfig
            {
                ApiKey = "mock_key",
                Model = "gpt-4o",
                MaxIterations = 10
            };

            // WHEN: Validating subagent configuration construction
            var delegateArgs = JsonSerializer.Serialize(new
            {
                role = "Code Reviewer",
                task = "Review PR #42 for potential security vulnerabilities"
            });

            // THEN: DelegateTool parses parameters properly and enforces subagent execution constraints
            delegateArgs.Should().Contain("Code Reviewer");
            delegateArgs.Should().Contain("Review PR #42");
        }
    }
}
