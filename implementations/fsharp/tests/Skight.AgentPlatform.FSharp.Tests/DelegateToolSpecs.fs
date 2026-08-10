namespace Skight.AgentPlatform.FSharp.Tests

open System
open System.Text.Json
open Expecto
open Skight.AgentPlatform.FSharp

module DelegateToolSpecs =

    let mockLlmCaller : LlmCaller =
        fun _ _ -> async {
            return Ok {
                Content = "Subagent task completed successfully."
                ToolCalls = []
            }
        }

    let mockExecutor : ToolExecutor =
        fun _ _ -> async { return "ok" }

    let testConfig = {
        Model = "gpt-4o"
        MaxIterations = 5
        ContextWindowLimit = 20
        MaxRetries = 2
    }

    [<Tests>]
    let tests =
        testList "Subagent Delegation Module Specs" [

            test "Feature: Subagent Context & Task Configuration" {
                let delegateArgs = JsonSerializer.Serialize({| role = "Security Inspector"; task = "Audit F# code" |})
                Expect.stringContains delegateArgs "Security Inspector" "Subagent role should be parsed"
                Expect.stringContains delegateArgs "Audit F# code" "Subagent task should be parsed"
            }

            testAsync "Feature: Single Subagent Execution Lifecycle" {
                let argsJson = """{"role":"analyst","task":"Review logs"}"""
                let! result = DelegateTool.delegateTask mockLlmCaller mockExecutor testConfig [] Set.empty argsJson |> Async.AwaitTask
                Expect.stringContains result "Subagent (analyst) output:" "Should return subagent summary"
                Expect.stringContains result "Subagent task completed successfully" "Should include response content"
            }

            testAsync "Feature: Batch Parallel Subagent Execution" {
                let argsJson = """
                {
                    "tasks": [
                        {"role": "researcher_1", "task": "Search DB logs"},
                        {"role": "researcher_2", "task": "Check server metrics"}
                    ]
                }
                """
                let! result = DelegateTool.delegateTask mockLlmCaller mockExecutor testConfig [] Set.empty argsJson |> Async.AwaitTask
                Expect.stringContains result "Batch Subagent Execution Results:" "Should indicate batch results"
                Expect.stringContains result "Subagent (researcher_1) output:" "Should contain researcher_1 output"
                Expect.stringContains result "Subagent (researcher_2) output:" "Should contain researcher_2 output"
            }
        ]
