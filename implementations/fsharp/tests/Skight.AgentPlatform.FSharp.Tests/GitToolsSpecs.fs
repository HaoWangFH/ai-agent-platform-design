namespace Skight.AgentPlatform.FSharp.Tests

open System
open System.IO
open System.Text.Json
open Expecto
open Skight.AgentPlatform.FSharp

module GitToolsSpecs =

    [<Tests>]
    let tests =
        testList "Git Automation Module Specs" [
            
            testAsync "Feature: Git Status, Commit & Push - Stages and commits changes" {
                let testRepoDir = Path.Combine(Path.GetTempPath(), sprintf "test_git_repo_fs_%s" (Guid.NewGuid().ToString("N")))
                Directory.CreateDirectory(testRepoDir) |> ignore

                try
                    let! _ = TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" init" testRepoDir)
                    let! _ = TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" config user.name \"TestUser\"" testRepoDir)
                    let! _ = TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" config user.email \"test@example.com\"" testRepoDir)

                    let filePath = Path.Combine(testRepoDir, "feature.txt")
                    File.WriteAllText(filePath, "New feature implementation code")

                    let! statusOutput = GitTools.gitStatus testRepoDir |> Async.AwaitTask
                    Expect.stringContains statusOutput "feature.txt" "Git status should report untracked file"

                    let commitArgs = JsonSerializer.Serialize({| message = "Implement new F# feature" |})
                    let! commitOutput = GitTools.gitCommit testRepoDir commitArgs |> Async.AwaitTask
                    Expect.isFalse (commitOutput.StartsWith("Error")) "Commit output should not be error"

                    let! logOutput = TerminalTool.executeCommandDefaultAsync (sprintf "git -C \"%s\" log -1" testRepoDir)
                    Expect.stringContains logOutput "Implement new F# feature" "Git log should record the commit"
                finally
                    if Directory.Exists(testRepoDir) then
                        try Directory.Delete(testRepoDir, true) with _ -> ()
            }
        ]
