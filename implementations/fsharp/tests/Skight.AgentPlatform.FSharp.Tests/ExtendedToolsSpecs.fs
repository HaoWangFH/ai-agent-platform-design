namespace Skight.AgentPlatform.FSharp.Tests

open System.IO
open System.Text.Json
open Expecto
open Skight.AgentPlatform.FSharp

module ExtendedToolsSpecs =

    [<Tests>]
    let tests =
        testList "Media and Automation Extended Tools Specs" [

            testAsync "Feature: Inspect Image File and Return Data URI" {
                let workspace = Directory.GetCurrentDirectory()
                let imgPath = "sample_test.png"
                let fullPath = Path.Combine(workspace, imgPath)
                File.WriteAllBytes(fullPath, [| 0x89uy; 0x50uy; 0x4Euy; 0x47uy |])
                let jsonArg = JsonSerializer.Serialize({| path = imgPath |})

                let! result = MediaTools.inspectImageAsync workspace jsonArg |> Async.AwaitTask
                if File.Exists(fullPath) then File.Delete(fullPath)

                Expect.stringContains result "image/png" "Response should contain MIME type"
                Expect.stringContains result "data:image/png;base64" "Response should contain base64 URI"
            }

            testAsync "Feature: Schedule Automation Timer Task" {
                let jsonArg = JsonSerializer.Serialize({| seconds = 1; prompt = "Perform backup" |})
                let! result = AutomationTools.scheduleTimerAsync jsonArg |> Async.AwaitTask
                Expect.stringContains result "Timer task #" "Confirmation should state task ID"
                Expect.stringContains result "Perform backup" "Confirmation should state prompt"
            }
        ]
