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

    let private isWindows = Environment.OSVersion.Platform = PlatformID.Win32NT

    [<Tests>]
    let task3SpecTests =
        testList "Task 3: Core Tools & Safe Execution Environment Specification Tests" [

            testList "3.1 Security & Sandbox Module Tests (ToolSecurity.fs)" [
                test "Path guard blocks relative traversal outside sandbox" {
                    let workspace = ensureWorkspace ()
                    match ToolSecurity.validatePathInSandbox workspace "../../etc/passwd" with
                    | Error err -> Expect.stringContains err "Access denied" "Relative traversal must be rejected"
                    | Ok path -> failtestf "Expected relative traversal to be blocked, but got: %s" path
                }

                test "Path guard blocks absolute paths outside sandbox" {
                    let workspace = ensureWorkspace ()
                    let outsidePath = if isWindows then @"C:\Windows\System32\drivers\etc\hosts" else "/etc/passwd"
                    match ToolSecurity.validatePathInSandbox workspace outsidePath with
                    | Error err -> Expect.stringContains err "Access denied" "Absolute path outside workspace must be rejected"
                    | Ok path -> failtestf "Expected absolute path outside workspace to be blocked, but got: %s" path
                }

                test "Path guard permits valid nested paths inside sandbox" {
                    let workspace = ensureWorkspace ()
                    match ToolSecurity.validatePathInSandbox workspace "src/deep/nested/file.txt" with
                    | Ok fullPath ->
                        Expect.isTrue (fullPath.StartsWith(Path.GetFullPath(workspace), StringComparison.OrdinalIgnoreCase)) "Valid path must stay inside workspace"
                    | Error err -> failtestf "Valid inside path should be permitted, got error: %s" err
                }

                test "Path guard rejects empty workspace or target path" {
                    match ToolSecurity.validatePathInSandbox "" "some/path.txt" with
                    | Error err -> Expect.stringContains err "Workspace root cannot be empty" "Empty workspace root error"
                    | Ok _ -> failtest "Empty workspace root must fail"

                    match ToolSecurity.validatePathInSandbox (ensureWorkspace ()) "" with
                    | Error err -> Expect.stringContains err "Target path cannot be empty" "Empty target path error"
                    | Ok _ -> failtest "Empty target path must fail"
                }
            ]

            testList "3.2 File Operations Toolset Tests (FileTools.fs)" [
                testAsync "writeFileTool creates file and auto-creates directory structure" {
                    let workspace = ensureWorkspace ()
                    let args = """{"path":"sub/dir/output.txt","content":"file operations test"}"""
                    let! writeRes = FileTools.writeFileTool workspace args
                    Expect.stringContains writeRes "Wrote" "Write operation should report written characters"

                    let fullPath = Path.Combine(workspace, "sub", "dir", "output.txt")
                    Expect.isTrue (File.Exists(fullPath)) "File must exist on disk after writeFileTool"
                    let text = File.ReadAllText(fullPath)
                    Expect.equal text "file operations test" "File content must match written string"
                }

                testAsync "readFileTool reads existing file and handles non-existent file gracefully" {
                    let workspace = ensureWorkspace ()
                    let writeArgs = """{"path":"sample.txt","content":"hello reader"}"""
                    let! _ = FileTools.writeFileTool workspace writeArgs

                    let readArgs = """{"path":"sample.txt"}"""
                    let! readRes = FileTools.readFileTool workspace readArgs
                    Expect.equal readRes "hello reader" "readFileTool should return file contents"

                    let missingArgs = """{"path":"non_existent.txt"}"""
                    let! missingRes = FileTools.readFileTool workspace missingArgs
                    Expect.stringContains missingRes "not found" "Non-existent file read should return not found error"
                }

                testAsync "readFileTool respects max_bytes option" {
                    let workspace = ensureWorkspace ()
                    let longContent = String.replicate 50 "0123456789"
                    let writeArgs = sprintf """{"path":"long.txt","content":"%s"}""" longContent
                    let! _ = FileTools.writeFileTool workspace writeArgs

                    let readArgs = """{"path":"long.txt","max_bytes":50}"""
                    let! readRes = FileTools.readFileTool workspace readArgs
                    Expect.stringContains readRes "[Output truncated:" "readFileTool must truncate output when max_bytes is exceeded"
                }

                testAsync "editFileTool search-and-replace mode updates target substring" {
                    let workspace = ensureWorkspace ()
                    let writeArgs = """{"path":"config.json","content":"{\"env\":\"development\",\"port\":8080}"}"""
                    let! _ = FileTools.writeFileTool workspace writeArgs

                    let editArgs = """{"path":"config.json","search":"\"development\"","replace":"\"production\""}"""
                    let! editRes = FileTools.editFileTool workspace editArgs
                    Expect.stringContains editRes "Applied 1 edit(s)" "editFileTool search-replace should succeed"

                    let! readRes = FileTools.readFileTool workspace """{"path":"config.json"}"""
                    Expect.stringContains readRes "\"production\"" "Edited content must be persisted"
                }

                testAsync "editFileTool diff block SEARCH/REPLACE patch mode applies changes" {
                    let workspace = ensureWorkspace ()
                    let originalText = "header\nline1: original\nline2: unchanged\nfooter"
                    let writeArgs = sprintf """{"path":"patch_target.txt","content":"%s"}""" (originalText.Replace("\n", "\\n"))
                    let! _ = FileTools.writeFileTool workspace writeArgs

                    let patchText = "<<<<<<< SEARCH\\nline1: original\\n=======\\nline1: patched_content\\n>>>>>>> REPLACE"
                    let editArgs = sprintf """{"path":"patch_target.txt","patch":"%s"}""" patchText
                    let! editRes = FileTools.editFileTool workspace editArgs
                    Expect.stringContains editRes "Applied 1 edit(s)" "Diff block patch must apply successfully"

                    let! readRes = FileTools.readFileTool workspace """{"path":"patch_target.txt"}"""
                    Expect.stringContains readRes "line1: patched_content" "Patched line should be present"
                }

                testAsync "editFileTool returns informative error when search or patch text is not found" {
                    let workspace = ensureWorkspace ()
                    let writeArgs = """{"path":"target.txt","content":"simple text"}"""
                    let! _ = FileTools.writeFileTool workspace writeArgs

                    let editArgs = """{"path":"target.txt","search":"missing_string","replace":"replacement"}"""
                    let! editRes = FileTools.editFileTool workspace editArgs
                    Expect.stringContains editRes "Search text not found" "Informative error on missing search string"
                }

                testAsync "File tools enforce sandbox path security" {
                    let workspace = ensureWorkspace ()
                    let readRes = FileTools.readFileTool workspace """{"path":"../../etc/passwd"}""" |> Async.RunSynchronously
                    Expect.stringContains readRes "Access denied" "readFileTool must block path traversal"

                    let writeRes = FileTools.writeFileTool workspace """{"path":"../../etc/passwd","content":"test"}""" |> Async.RunSynchronously
                    Expect.stringContains writeRes "Access denied" "writeFileTool must block path traversal"

                    let editRes = FileTools.editFileTool workspace """{"path":"../../etc/passwd","search":"a","replace":"b"}""" |> Async.RunSynchronously
                    Expect.stringContains editRes "Access denied" "editFileTool must block path traversal"
                }
            ]

            testList "3.3 Output Limit & Truncation Guard Tests (ToolSecurity.fs)" [
                test "truncateOutput caps character/byte limits and adds truncation marker" {
                    let text = String.replicate 500 "DATA_ROW\n"
                    let result = ToolSecurity.truncateOutput 200 text
                    Expect.stringContains result "[Output truncated:" "Truncation marker present"
                    Expect.isTrue (result.Length <= 350) "Truncated length should be capped near maxBytes plus marker"
                }

                test "truncateOutputWithLimits caps line count and byte limits" {
                    let text = [ for i in 1..700 -> sprintf "line_%d" i ] |> String.concat "\n"
                    let result = ToolSecurity.truncateOutputWithLimits 100000 500 text
                    Expect.stringContains result "[Output truncated: 200 lines hidden" "Line count truncation marker present"
                    let lineCount = result.Replace("\r\n", "\n").Split('\n').Length
                    Expect.isTrue (lineCount <= 525) "Output line count must be capped near maxLines"
                }
            ]

            testList "3.4 Process Execution Engine Tests (TerminalTool.fs)" [
                testAsync "executeCommandAsync executes standard shell command and returns output" {
                    let echoCmd = if isWindows then "echo FSharpTerminalTest" else "echo FSharpTerminalTest"
                    let! output = TerminalTool.executeCommandAsync 5000 1024 echoCmd
                    Expect.stringContains output "FSharpTerminalTest" "Command execution should return stdout"
                }

                testAsync "executeCommandAsync handles process timeout and terminates hanging command" {
                    let longRunning = if isWindows then "ping 127.0.0.1 -n 6 > nul" else "sleep 5"
                    let! output = TerminalTool.executeCommandAsync 200 1024 longRunning
                    Expect.stringContains output "timed out" "Long-running command must timeout and be terminated"
                }

                testAsync "Background command lifecycle: start, poll output, and stop" {
                    let bgCmd = if isWindows then "ping 127.0.0.1 -n 10 > nul" else "sleep 10"
                    match TerminalTool.startBackgroundCommand bgCmd with
                    | Error err -> failtestf "Failed to start background command: %s" err
                    | Ok handle ->
                        Expect.isFalse (String.IsNullOrWhiteSpace(handle.Id)) "Background command ID must be valid"

                        let output = TerminalTool.getBackgroundCommandOutput handle.Id 1024
                        Expect.stringContains output handle.Id "Background output should contain command ID"

                        let stopMsg = TerminalTool.stopBackgroundCommand handle.Id
                        Expect.stringContains stopMsg "stopped" "Background command should be stopped successfully"

                        TerminalTool.cleanupCompletedBackgroundCommands ()
                }
            ]

            testList "3.5 Interactive Approval Guard Tests (ApprovalGuard.fs)" [
                test "isHighRiskCommand classifies dangerous commands correctly" {
                    Expect.isTrue (ApprovalGuard.isHighRiskCommand "rm -rf /") "rm -rf must be high risk"
                    Expect.isTrue (ApprovalGuard.isHighRiskCommand "del /f C:\\file.txt") "del /f must be high risk"
                    Expect.isTrue (ApprovalGuard.isHighRiskCommand "sudo apt-get update") "sudo must be high risk"
                    Expect.isTrue (ApprovalGuard.isHighRiskCommand "format D:") "format must be high risk"
                    Expect.isFalse (ApprovalGuard.isHighRiskCommand "echo hello") "echo hello is safe"
                    Expect.isFalse (ApprovalGuard.isHighRiskCommand "dotnet build") "dotnet build is safe"
                }

                test "isHighRiskFileEdit classifies sensitive files and secret payload keywords" {
                    Expect.isTrue (ApprovalGuard.isHighRiskFileEdit ".env" "KEY=VAL") ".env file edit must be high risk"
                    Expect.isTrue (ApprovalGuard.isHighRiskFileEdit "config/secrets.json" "{}") "secrets.json must be high risk"
                    Expect.isTrue (ApprovalGuard.isHighRiskFileEdit "app.fs" "let db_password = \"123\"") "Content with password keyword must be high risk"
                    Expect.isFalse (ApprovalGuard.isHighRiskFileEdit "app.fs" "let count = 42") "Normal code edit is safe"
                }

                testAsync "requireCommandApproval allows on Approved and rejects on Denied" {
                    let approvePrompt : ApprovalGuard.ApprovalPrompt =
                        fun _ -> async { return ApprovalGuard.Approved }
                    let denyPrompt : ApprovalGuard.ApprovalPrompt =
                        fun _ -> async { return ApprovalGuard.Denied "User cancelled execution" }

                    let! approveRes = ApprovalGuard.requireCommandApproval approvePrompt "rm -rf /tmp/data"
                    Expect.equal approveRes (Ok ()) "Approved command should succeed"

                    let! denyRes = ApprovalGuard.requireCommandApproval denyPrompt "rm -rf /tmp/data"
                    match denyRes with
                    | Error err -> Expect.equal err "User cancelled execution" "Denial reason should be returned"
                    | Ok () -> failtest "Denied command must return Error"
                }

                testAsync "requireFileEditApproval prompts for sensitive file edit" {
                    let mutable promptedAction = ""
                    let capturePrompt : ApprovalGuard.ApprovalPrompt =
                        fun req -> async {
                            promptedAction <- req.Action
                            return ApprovalGuard.Approved
                        }

                    let! res = ApprovalGuard.requireFileEditApproval capturePrompt ".env" "API_KEY=secret_token"
                    Expect.equal res (Ok ()) "Approved sensitive edit should succeed"
                    Expect.equal promptedAction "edit_file" "Prompt request should contain edit_file action"
                }
            ]
        ]

