namespace Skight.AgentPlatform.FSharp.Tests

open System
open System.Text.Json
open Expecto
open Skight.AgentPlatform.FSharp

module DelegateToolSpecs =

    [<Tests>]
    let tests =
        testList "Subagent Delegation Module Specs" [

            test "Feature: Subagent Context & Task Configuration" {
                let delegateArgs = JsonSerializer.Serialize({| role = "Security Inspector"; task = "Audit F# code" |})
                Expect.stringContains delegateArgs "Security Inspector" "Subagent role should be parsed"
                Expect.stringContains delegateArgs "Audit F# code" "Subagent task should be parsed"
            }
        ]
