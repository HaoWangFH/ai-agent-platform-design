namespace Skight.AgentPlatform.FSharp.Tests

open Expecto
open Skight.AgentPlatform.FSharp

module SequentialToolWorkflowSpec =

    [<Tests>]
    let sequentialToolWorkflowTests =
        testList "Multi-Turn Sequential Tool Execution Expecto Spec" [

            testAsync "SPEC: Multi-turn sequential tool call workflow (LLM -> Tool 1 -> LLM -> Tool 2 -> LLM -> Text)" {
                let callCounter = ref 0

                let mockLlmCaller : LlmCaller =
                    fun _ _ -> async {
                        incr callCounter
                        match !callCounter with
                        | 1 ->
                            return
                                Ok {
                                    Content = ""
                                    ToolCalls = [ { Id = "call_weather_123"; Name = "get_weather"; ArgumentsJson = "{\"location\":\"Tokyo\"}" } ]
                                }
                        | 2 ->
                            return
                                Ok {
                                    Content = ""
                                    ToolCalls = [ { Id = "call_contact_456"; Name = "search_contacts"; ArgumentsJson = "{\"name\":\"Alice\"}" } ]
                                }
                        | 3 ->
                            return
                                Ok {
                                    Content = "Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com)."
                                    ToolCalls = []
                                }
                        | _ -> return Error (ApiCallFailed "Unexpected LLM call beyond expected sequence")
                    }

                let mockExecutor : ToolExecutor =
                    fun name _ -> async {
                        match name with
                        | "get_weather" -> return "25°C, Sunny"
                        | "search_contacts" -> return "alice@example.com"
                        | _ -> return sprintf "Unknown tool %s" name
                    }

                let registeredNamesSet = Set.ofList [ "get_weather"; "search_contacts" ]

                let initialState : TurnState = {
                    Messages = [
                        SystemMessage "You are a helpful assistant."
                        UserMessage "Find weather in Tokyo and notify Alice."
                    ]
                    ApiCalls = 0
                    EmptyContentRetries = 0
                    Command = RunTurn
                    Config = { MaxIterations = 5; MaxRetries = 2; ContextWindowLimit = 10; Model = "test-model" }
                }

                let! result = AgentPipeline.runTurnLoop mockLlmCaller mockExecutor [] registeredNamesSet initialState

                match result.Outcome with
                | TurnOutcome.Completed finalResponse ->
                    let actual = {| ApiCalls = result.ApiCalls; FinalResponse = finalResponse; MessageCount = result.Messages.Length |}
                    let expected = {| ApiCalls = 3; FinalResponse = "Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com)."; MessageCount = 7 |}
                    Expect.equal actual expected "Expected successful multi-turn completion"
                | outcome ->
                    failtestf "Expected completed outcome, got %A" outcome

                match result.Messages.[2], result.Messages.[3], result.Messages.[4], result.Messages.[5], result.Messages.[6] with
                | AssistantMessage (_, firstCalls), ToolMessage (firstCallId, firstResult), AssistantMessage (_, secondCalls), ToolMessage (secondCallId, secondResult), AssistantMessage (finalText, []) ->
                    let actual = {|
                        FirstToolName = firstCalls |> List.tryHead |> Option.map (fun c -> c.Name)
                        FirstToolResult = firstResult
                        FirstToolCallId = firstCallId
                        SecondToolName = secondCalls |> List.tryHead |> Option.map (fun c -> c.Name)
                        SecondToolResult = secondResult
                        SecondToolCallId = secondCallId
                        FinalAssistantText = finalText
                    |}

                    let expected = {|
                        FirstToolName = Some "get_weather"
                        FirstToolResult = "25°C, Sunny"
                        FirstToolCallId = "call_weather_123"
                        SecondToolName = Some "search_contacts"
                        SecondToolResult = "alice@example.com"
                        SecondToolCallId = "call_contact_456"
                        FinalAssistantText = "Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com)."
                    |}

                    Expect.equal actual expected "Expected sequential tool-call message transcript"
                | _ ->
                    failtest "Unexpected message shape for sequential tool workflow"
            }
        ]
