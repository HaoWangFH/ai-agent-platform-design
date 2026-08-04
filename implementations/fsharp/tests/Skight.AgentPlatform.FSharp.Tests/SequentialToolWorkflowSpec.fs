namespace Skight.AgentPlatform.FSharp.Tests

open System
open Azure.AI.OpenAI
open Expecto
open Skight.AgentPlatform.FSharp

module SequentialToolWorkflowSpec =

    let private createToolCallCompletions (toolCallId: string) (toolName: string) (argumentsJson: string) =
        let toolCall = ChatCompletionsFunctionToolCall(toolCallId, toolName, argumentsJson)
        let responseMsg = AzureOpenAIModelFactory.ChatResponseMessage(ChatRole.Assistant, "", [ toolCall ])
        let choice = AzureOpenAIModelFactory.ChatChoice(message = responseMsg, index = 0, finishReason = Nullable(CompletionsFinishReason.ToolCalls))
        AzureOpenAIModelFactory.ChatCompletions(null, DateTimeOffset.UtcNow, [ choice ], null, null, null)

    let private createTextCompletions (textResponse: string) =
        let responseMsg = AzureOpenAIModelFactory.ChatResponseMessage(ChatRole.Assistant, textResponse, null)
        let choice = AzureOpenAIModelFactory.ChatChoice(message = responseMsg, index = 0, finishReason = Nullable(CompletionsFinishReason.Stopped))
        AzureOpenAIModelFactory.ChatCompletions(null, DateTimeOffset.UtcNow, [ choice ], null, null, null)

    [<Tests>]
    let sequentialToolWorkflowTests =
        testList "Multi-Turn Sequential Tool Execution Expecto Spec" [

            testAsync "SPEC: Multi-turn sequential tool call workflow (LLM -> Tool 1 -> LLM -> Tool 2 -> LLM -> Text)" {
                let callCounter = ref 0

                let mockLlmCaller : LlmCaller =
                    fun _ msgs -> async {
                        incr callCounter
                        match !callCounter with
                        | 1 -> return Ok (createToolCallCompletions "call_weather_123" "get_weather" "{\"location\":\"Tokyo\"}")
                        | 2 -> return Ok (createToolCallCompletions "call_contact_456" "search_contacts" "{\"name\":\"Alice\"}")
                        | 3 -> return Ok (createTextCompletions "Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com).")
                        | _ -> return Error "Unexpected LLM call beyond expected sequence"
                    }

                let mockExecutor : ToolExecutor =
                    fun name args -> async {
                        match name with
                        | "get_weather" -> return "25°C, Sunny"
                        | "search_contacts" -> return "alice@example.com"
                        | _ -> return sprintf "Unknown tool %s" name
                    }

                let registeredNamesSet = Set.ofList [ "get_weather"; "search_contacts" ]

                let initialState : TurnState = {
                    Messages = [
                        ChatRequestSystemMessage("You are a helpful assistant.") :> ChatRequestMessage
                        ChatRequestUserMessage("Find weather in Tokyo and notify Alice.") :> ChatRequestMessage
                    ]
                    ApiCalls = 0
                    EmptyContentRetries = 0
                    InterruptRequested = false
                    Config = { MaxIterations = 5; MaxRetries = 2; ContextWindowLimit = 10; Model = "test-model" }
                }

                let! result = AgentPipeline.runTurnLoop mockLlmCaller mockExecutor [] registeredNamesSet initialState

                Expect.isTrue result.Completed "Turn should complete successfully"
                Expect.isFalse result.Failed "Turn should not fail"
                Expect.equal result.ApiCalls 3 "Should make exactly 3 API calls"
                Expect.equal result.FinalResponse "Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com)." "Final response text match"
                Expect.equal result.Messages.Length 7 "Message history length should be 7"

                let asstMsg1 = result.Messages.[2] :?> ChatRequestAssistantMessage
                Expect.isNotNull asstMsg1.ToolCalls "Assistant msg 1 should have tool calls"
                Expect.equal (asstMsg1.ToolCalls.[0] :?> ChatCompletionsFunctionToolCall).Name "get_weather" "First tool call name"

                let toolMsg1 = result.Messages.[3] :?> ChatRequestToolMessage
                Expect.equal toolMsg1.Content "25°C, Sunny" "First tool result content"
                Expect.equal toolMsg1.ToolCallId "call_weather_123" "First tool call ID"

                let asstMsg2 = result.Messages.[4] :?> ChatRequestAssistantMessage
                Expect.isNotNull asstMsg2.ToolCalls "Assistant msg 2 should have tool calls"
                Expect.equal (asstMsg2.ToolCalls.[0] :?> ChatCompletionsFunctionToolCall).Name "search_contacts" "Second tool call name"

                let toolMsg2 = result.Messages.[5] :?> ChatRequestToolMessage
                Expect.equal toolMsg2.Content "alice@example.com" "Second tool result content"
                Expect.equal toolMsg2.ToolCallId "call_contact_456" "Second tool call ID"

                let asstMsg3 = result.Messages.[6] :?> ChatRequestAssistantMessage
                Expect.equal asstMsg3.Content "Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com)." "Final assistant message content"
                return ()
            }
        ]
