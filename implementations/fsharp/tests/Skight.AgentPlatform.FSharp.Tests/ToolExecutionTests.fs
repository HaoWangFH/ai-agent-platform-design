namespace Skight.AgentPlatform.FSharp.Tests

open System
open System.IO
open Expecto
open Skight.AgentPlatform.FSharp

module ToolExecutionTests =
    let private testWorkspace = Path.Combine(Path.GetTempPath(), "fsharp_tool_sandbox", Guid.NewGuid().ToString("N"))

    let private ensureWorkspace () =
        Directory.CreateDirectory(testWorkspace) |> ignore
        testWorkspace

    [<Tests>]
    let toolExecutionTests =
        testList "Task 3 Tool Security & Execution Tests" [
            test "Path guard blocks traversal outside sandbox" {
                let workspace = ensureWorkspace ()
                match ToolSecurity.validatePathInSandbox workspace "../../etc/passwd" with
                | Error err -> Expect.stringContains err "Access denied" "Traversal must be rejected"
                | Ok _ -> failtest "Expected traversal path to be blocked"
            }

            test "Output truncation applies line and byte caps" {
                let text = [ for i in 1..700 -> sprintf "line_%d" i ] |> String.concat "\n"
                let result = ToolSecurity.truncateOutputWithLimits 1024 500 text

                Expect.isTrue (result.Contains("[Output truncated:")) "Expected truncation marker"
                let lineCount = result.Replace("\r\n", "\n").Split('\n').Length
                Expect.isTrue (lineCount <= 520) "Truncated output should stay near configured line bound"
            }

            testAsync "File tools write/read/edit within sandbox" {
                let workspace = ensureWorkspace ()

                let writeArgs = """{"path":"notes/test.txt","content":"hello world"}"""
                let! writeResult = FileTools.writeFileTool workspace writeArgs
                Expect.stringContains writeResult "Wrote" "Write should succeed"

                let readArgs = """{"path":"notes/test.txt"}"""
                let! readResult = FileTools.readFileTool workspace readArgs
                Expect.equal readResult "hello world" "Read should return written content"

                let editArgs = """{"path":"notes/test.txt","search":"world","replace":"agent"}"""
                let! editResult = FileTools.editFileTool workspace editArgs
                Expect.stringContains editResult "Applied" "Edit should succeed"

                let! reread = FileTools.readFileTool workspace readArgs
                Expect.equal reread "hello agent" "Edited content should be persisted"
            }

            testAsync "Terminal command times out and gets terminated" {
                let isWindows = Environment.OSVersion.Platform = PlatformID.Win32NT
                let longRunning = if isWindows then "ping 127.0.0.1 -n 6 > nul" else "sleep 5"

                let! output = TerminalTool.executeCommandAsync 200 1024 longRunning
                Expect.stringContains output "timed out" "Long-running command must timeout"
            }

            testAsync "Approval guard blocks denied high-risk command" {
                let denyPrompt : ApprovalGuard.ApprovalPrompt =
                    fun _ -> async { return ApprovalGuard.Denied "blocked" }

                let! result = ApprovalGuard.requireCommandApproval denyPrompt "rm -rf /tmp/test"
                match result with
                | Ok () -> failtest "Expected high-risk command approval to be denied"
                | Error err -> Expect.equal err "blocked" "Denial reason should propagate"
            }
        ]
