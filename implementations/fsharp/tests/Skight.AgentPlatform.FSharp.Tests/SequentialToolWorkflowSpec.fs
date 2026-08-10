namespace Skight.AgentPlatform.FSharp.Tests

open Expecto
open Skight.AgentPlatform.FSharp

module SequentialToolWorkflowSpec =

    [<Tests>]
    let sequentialToolWorkflowTests =
        testList "Multi-Turn Sequential Tool Execution Expecto Spec" [

            testAsync "SPEC: Multi-turn sequential tool call workflow (LLM -> Tool 1 -> LLM -> Tool 2 -> LLM -> Text)" {
                let callCounter = ref 0

                let weatherId = match ToolCallId.create "call_weather_123" with Ok x -> x | _ -> failwith ""
                let weatherName = match ToolName.create "get_weather" with Ok x -> x | _ -> failwith ""
                let contactId = match ToolCallId.create "call_contact_456" with Ok x -> x | _ -> failwith ""
                let contactName = match ToolName.create "search_contacts" with Ok x -> x | _ -> failwith ""

                let mockLlmCaller : LlmCaller =
                    fun _ _ -> async {
                        incr callCounter
                        match !callCounter with
                        | 1 ->
                            return
                                Ok {
                                    Content = ""
                                    ToolCalls = [ { Id = weatherId; Name = weatherName; ArgumentsJson = "{\"location\":\"Tokyo\"}" } ]
                                }
                        | 2 ->
                            return
                                Ok {
                                    Content = ""
                                    ToolCalls = [ { Id = contactId; Name = contactName; ArgumentsJson = "{\"name\":\"Alice\"}" } ]
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
                        match ToolName.value name with
                        | "get_weather" -> return "25°C, Sunny"
                        | "search_contacts" -> return "alice@example.com"
                        | _ -> return sprintf "Unknown tool %s" (ToolName.value name)
                    }

                let registeredNamesSet =
                    [ "get_weather"; "search_contacts" ]
                    |> List.choose (fun n -> match ToolName.create n with Ok x -> Some x | _ -> None)
                    |> Set.ofList

                let config = { MaxIterations = 5; MaxRetries = 2; ContextWindowLimit = 10; Model = "test-model" }
                let initialSession : AgentSessionState = {
                    Messages = [ SystemMessage "You are a helpful assistant." ]
                    PendingCommand = RunTurn
                    SteeringQueue = System.Collections.Concurrent.ConcurrentQueue<string>()
                }

                let! result, nextSession = AgentRunner.runTurnAsync mockLlmCaller mockExecutor config "Find weather in Tokyo and notify Alice." initialSession [] registeredNamesSet

                match result.Outcome with
                | TurnOutcome.Completed finalResponse ->
                    let actual = {| ApiCalls = result.ApiCalls; FinalResponse = finalResponse; MessageCount = result.Messages.Length |}
                    let expected = {| ApiCalls = 3; FinalResponse = "Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com)."; MessageCount = 7 |}
                    Expect.equal actual expected "Expected successful multi-turn completion"
                | outcome ->
                    failtestf "Expected completed outcome, got %A" outcome

                match nextSession.Messages.[2], nextSession.Messages.[3], nextSession.Messages.[4], nextSession.Messages.[5], nextSession.Messages.[6] with
                | AssistantMessage (_, firstCalls), ToolMessage (firstCallId, firstResult), AssistantMessage (_, secondCalls), ToolMessage (secondCallId, secondResult), AssistantMessage (finalText, []) ->
                    let actual = {|
                        FirstToolName = firstCalls |> List.tryHead |> Option.map (fun c -> ToolName.value c.Name)
                        FirstToolResult = firstResult
                        FirstToolCallId = ToolCallId.value firstCallId
                        SecondToolName = secondCalls |> List.tryHead |> Option.map (fun c -> ToolName.value c.Name)
                        SecondToolResult = secondResult
                        SecondToolCallId = ToolCallId.value secondCallId
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
