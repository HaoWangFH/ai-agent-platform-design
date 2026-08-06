namespace Skight.AgentPlatform.FSharp.Tests

open System.IO
open Expecto
open Skight.AgentPlatform.FSharp

module SystemInfoToolSpecs =

    [<Tests>]
    let tests =
        testList "System Info Tool Specs" [

            testAsync "Feature: Fetch Environment and System Metadata" {
                let workspace = Directory.GetCurrentDirectory()
                let! jsonRes = SystemInfoTool.getSystemInfoAsync workspace |> Async.AwaitTask
                Expect.stringContains jsonRes "os" "Response should contain OS metadata"
                Expect.stringContains jsonRes "workspace" "Response should contain workspace path"
            }
        ]
