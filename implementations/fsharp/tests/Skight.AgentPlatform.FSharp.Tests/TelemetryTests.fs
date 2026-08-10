module Skight.AgentPlatform.FSharp.Tests.TelemetryTests

open System
open System.IO
open Expecto
open Skight.AgentPlatform.FSharp

[<Tests>]
let tests =
    testSequenced (testList "Telemetry Tests" [
        testCaseAsync "AgentTelemetry logs events asynchronously when enabled" (async {
            let testDir = Path.Combine(Path.GetTempPath(), sprintf "fsharp_telemetry_test_%s" (Guid.NewGuid().ToString("N")))
            AgentTelemetry.IsEnabled <- true
            AgentTelemetry.LogDirectory <- testDir

            let sessionId = sprintf "fs_sess_%s" (Guid.NewGuid().ToString("N"))
            let spanId = Guid.NewGuid().ToString("N")
            AgentTelemetry.trackTurnStart sessionId "fsharp_user" 1 "Run F# tests" (Some sessionId) (Some spanId)
            AgentTelemetry.trackToolExecution sessionId "fsharp_user" 1 "terminal_execute" 25L "{}" "Success" (Some sessionId) (Some spanId)
            AgentTelemetry.trackTurnEnd sessionId "fsharp_user" 1 120L "Tests completed" "completed" (Some sessionId) (Some spanId)
            AgentTelemetry.flush()

            let sessionDir = Path.Combine(testDir, sessionId)
            let compactPath = Path.Combine(sessionDir, "transcript.jsonl")
            let fullPath = Path.Combine(sessionDir, "transcript_full.jsonl")

            Expect.isTrue (File.Exists compactPath) "transcript.jsonl should exist"
            Expect.isTrue (File.Exists fullPath) "transcript_full.jsonl should exist"

            let! compactLines = File.ReadAllLinesAsync compactPath |> Async.AwaitTask
            Expect.equal compactLines.Length 3 "Should record 3 events in transcript.jsonl"
            Expect.isTrue (compactLines.[0].Contains("agent.turn.start")) "First event should be turn start"
            Expect.isTrue (compactLines.[1].Contains("tool.execution:terminal_execute")) "Second event should be tool execution"
            Expect.isTrue (compactLines.[2].Contains("agent.turn.end")) "Third event should be turn end"

            Directory.Delete(testDir, true)
        })

        testCaseAsync "AgentTelemetry produces zero logs when disabled" (async {
            let testDir = Path.Combine(Path.GetTempPath(), sprintf "fsharp_telemetry_test_disabled_%s" (Guid.NewGuid().ToString("N")))
            AgentTelemetry.IsEnabled <- false
            AgentTelemetry.LogDirectory <- testDir

            let sessionId = sprintf "disabled_fs_sess_%s" (Guid.NewGuid().ToString("N"))
            AgentTelemetry.trackTurnStart sessionId "fsharp_user" 1 "Disabled test" (Some sessionId) None

            do! Async.Sleep 100

            let sessionDir = Path.Combine(testDir, sessionId)
            Expect.isFalse (Directory.Exists sessionDir) "Session directory should not be created when disabled"
        })
    ])
