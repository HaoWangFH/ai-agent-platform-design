namespace Skight.AgentPlatform.FSharp.Tests

open System
open System.Text.Json
open Expecto
open Skight.AgentPlatform.FSharp

module MemoryToolSpecs =

    [<Tests>]
    let tests =
        testList "Memory Tool Module Specs" [

            testAsync "Feature: Store and Recall Memory" {
                let storeJson = JsonSerializer.Serialize({| key = "user_pref"; value = "Dark Theme" |})
                let recallJson = JsonSerializer.Serialize({| key = "user_pref" |})

                let! storeRes = MemoryTool.storeMemoryAsync storeJson |> Async.AwaitTask
                let! recallRes = MemoryTool.recallMemoryAsync recallJson |> Async.AwaitTask

                Expect.stringContains storeRes "user_pref" "Store response should mention key"
                Expect.stringContains recallRes "Dark Theme" "Recall response should contain stored value"
            }
        ]
