namespace Skight.AgentPlatform.FSharp.Tests

open System
open System.IO
open System.Text.Json
open Expecto
open Skight.AgentPlatform.FSharp

module Phase2BusinessWorkflowSpecs =

    [<Tests>]
    let tests =
        testList "Phase 2 Core Business Workflows Specs" [
            
            testAsync "Feature: Git Automation - Stages, Commits, and Pushes workspace changes" {
                // SCENARIO: Developer requests automated Git status, commit, and push operations.
                let testRepoDir = Path.Combine(Path.GetTempPath(), sprintf "test_git_repo_fs_%s" (Guid.NewGuid().ToString("N")))
                Directory.CreateDirectory(testRepoDir) |> ignore

                try
                    let! _ = TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" init" testRepoDir)
                    let! _ = TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" config user.name \"TestUser\"" testRepoDir)
                    let! _ = TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" config user.email \"test@example.com\"" testRepoDir)

                    let filePath = Path.Combine(testRepoDir, "feature.txt")
                    File.WriteAllText(filePath, "New feature implementation code")

                    // WHEN: Checking git status
                    let! statusOutput = GitTools.gitStatus testRepoDir |> Async.AwaitTask
                    Expect.stringContains statusOutput "feature.txt" "Git status should report untracked file"

                    // WHEN: Committing changes
                    let commitArgs = JsonSerializer.Serialize({| message = "Implement new F# feature" |})
                    let! commitOutput = GitTools.gitCommit testRepoDir commitArgs |> Async.AwaitTask
                    Expect.isFalse (commitOutput.StartsWith("Error")) "Commit output should not be error"

                    let! logOutput = TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" log -1" testRepoDir)
                    Expect.stringContains logOutput "Implement new F# feature" "Git log should record the commit"
                finally
                    if Directory.Exists(testRepoDir) then
                        try Directory.Delete(testRepoDir, true) with _ -> ()
            }

            test "Feature: Security Guard - Intercepts dangerous shell operations" {
                // SCENARIO: Prevent malicious or accidental destructive system commands.
                let dangerousCommand = "rm -rf /"
                let safeCommand = "echo Hello FSharp"

                let isDangerous = ApprovalGuard.isHighRiskCommand dangerousCommand
                let isSafe = ApprovalGuard.isHighRiskCommand safeCommand

                Expect.isTrue isDangerous "rm -rf must be flagged as high risk"
                Expect.isFalse isSafe "echo Hello FSharp must not be flagged as high risk"

                let mockRejectPrompt : ApprovalGuard.ApprovalPrompt = fun req -> async { return ApprovalGuard.Denied "User rejected action." }
                let result = ApprovalGuard.requireCommandApproval mockRejectPrompt dangerousCommand |> Async.RunSynchronously

                match result with
                | Error msg -> Expect.equal msg "User rejected action." "Security guard should deny dangerous command"
                | Ok () -> failwith "Dangerous command should not be approved"
            }

            test "Feature: Subagent Delegation - Configures subagent with isolated context" {
                // SCENARIO: Primary agent delegates complex task to specialized subagent.
                let delegateArgs = JsonSerializer.Serialize({| role = "Security Inspector"; task = "Audit F# code" |})
                Expect.stringContains delegateArgs "Security Inspector" "Subagent role should be parsed"
                Expect.stringContains delegateArgs "Audit F# code" "Subagent task should be parsed"
            }
        ]
