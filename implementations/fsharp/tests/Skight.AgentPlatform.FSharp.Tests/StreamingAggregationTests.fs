namespace Skight.AgentPlatform.FSharp.Tests

open System.Collections.Generic
open Expecto
open FSharp.Control
open Skight.AgentPlatform.FSharp

module StreamingAggregationTests =

    [<Tests>]
    let streamingAggregationTests =
        testList "Streaming aggregation tests" [
            testAsync "aggregateStreamResponse stitches text and tool call fragments" {
                let id = match ToolCallId.create "call_1" with Ok x -> x | _ -> failtest "invalid id"
                let name = match ToolName.create "get_weather" with Ok x -> x | _ -> failtest "invalid name"

                let stream : IAsyncEnumerable<StreamChunk> =
                    taskSeq {
                        yield TextDelta "Hello"
                        yield TextDelta " world"
                        yield ToolCallDelta(0, Some id, Some name, "{\"location\":")
                        yield ToolCallDelta(0, None, None, "\"Tokyo\"}")
                        yield StreamCompleted "stop"
                    }

                let! result = AgentPipeline.aggregateStreamResponse stream
                match result with
                | Ok response ->
                    Expect.equal response.Content "Hello world" "Expected text to be aggregated"
                    Expect.equal response.ToolCalls.Length 1 "Expected one tool call"
                    Expect.equal (ToolCallId.value response.ToolCalls.Head.Id) "call_1" "Expected tool call id"
                    Expect.equal (ToolName.value response.ToolCalls.Head.Name) "get_weather" "Expected tool name"
                    Expect.equal response.ToolCalls.Head.ArgumentsJson "{\"location\":\"Tokyo\"}" "Expected stitched JSON arguments"
                | Error err ->
                    failtestf "Expected Ok response, got %A" err
            }

            testAsync "streamToLlmResponse salvages partial text on stream drop" {
                let streamingCaller : StreamingLlmCaller =
                    fun _ _ ->
                        async {
                            let stream : IAsyncEnumerable<StreamChunk> =
                                taskSeq {
                                    yield TextDelta "Partial"
                                    yield TextDelta " answer"
                                }
                            return Ok stream
                        }

                let! result = AgentPipeline.streamToLlmResponse streamingCaller [] []
                match result with
                | Ok response ->
                    Expect.equal response.Content "Partial answer" "Expected partial text to be salvaged"
                    Expect.isEmpty response.ToolCalls "Expected no tool calls in salvage path"
                | Error err ->
                    failtestf "Expected salvaged Ok response, got %A" err
            }

            testAsync "streamToLlmResponse maps empty partial to NoChoicesReturned" {
                let streamingCaller : StreamingLlmCaller =
                    fun _ _ ->
                        async {
                            let stream : IAsyncEnumerable<StreamChunk> =
                                taskSeq { () }
                            return Ok stream
                        }

                let! result = AgentPipeline.streamToLlmResponse streamingCaller [] []
                match result with
                | Error NoChoicesReturned ->
                    ()
                | other ->
                    failtestf "Expected NoChoicesReturned, got %A" other
            }
        ]
