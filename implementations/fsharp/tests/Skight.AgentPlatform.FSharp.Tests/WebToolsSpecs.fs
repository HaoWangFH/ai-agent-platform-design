namespace Skight.AgentPlatform.FSharp.Tests

open System
open System.Text.Json
open Expecto
open Skight.AgentPlatform.FSharp

module WebToolsSpecs =

    [<Tests>]
    let tests =
        testList "Web Tools Module Specs" [
            
            testAsync "Feature: Fetch URL Content - Returns clean stripped text" {
                let urlJson = JsonSerializer.Serialize({| url = "https://httpbin.org/html" |})
                let! content = WebTools.fetchUrlContentAsync urlJson |> Async.AwaitTask
                Expect.isFalse (content.Contains("<html")) "Content should not contain raw HTML tags"
                Expect.isFalse (String.IsNullOrWhiteSpace(content)) "Content should not be empty"
            }
        ]
