namespace Skight.AgentPlatform.FSharp.Tests

open System
open Expecto
open Skight.AgentPlatform.FSharp

module MemoryStoreSpecs =

    [<Tests>]
    let tests =
        testList "Server-Ready Memory Store Specs" [

            testAsync "Feature: Store & Search User Memory Records" {
                let store = MemoryStoreFactory.createInMemory ()
                let userId = "user_123"

                do! store.StoreAsync userId "preferred_lang" "F# Functional Architecture"
                do! store.StoreAsync userId "rule_1" "Do not use cat in terminal"

                let query = { UserId = userId; SearchText = "Functional"; Vector = None; Limit = 5 }
                let! results = store.SearchAsync query

                Expect.equal results.Length 1 "Should return 1 matching memory record"
                Expect.equal results.Head.Key "preferred_lang" "Key should match"
                Expect.stringContains results.Head.Value "F# Functional Architecture" "Value should contain preference"
            }

            testAsync "Feature: Multi-Tenant User Isolation" {
                let store = MemoryStoreFactory.createInMemory ()
                do! store.StoreAsync "user_A" "secret" "User A secret info"
                do! store.StoreAsync "user_B" "secret" "User B secret info"

                let queryA = { UserId = "user_A"; SearchText = "secret"; Vector = None; Limit = 5 }
                let! resultsA = store.SearchAsync queryA

                Expect.equal resultsA.Length 1 "User A should get 1 record"
                Expect.equal resultsA.Head.Value "User A secret info" "User A should not see User B data"
            }
        ]
