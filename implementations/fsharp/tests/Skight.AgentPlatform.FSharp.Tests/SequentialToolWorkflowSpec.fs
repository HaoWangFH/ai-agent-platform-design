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

    let private (|AssistantMessage|_|) (msg: ChatRequestMessage) =
        match msg with
        | :? ChatRequestAssistantMessage as assistant -> Some assistant
        | _ -> None

    let private (|ToolMessage|_|) (msg: ChatRequestMessage) =
        match msg with
        | :? ChatRequestToolMessage as tool -> Some tool
        | _ -> None

    let private (|FunctionToolCall|_|) (toolCall: ChatCompletionsToolCall) =
        match toolCall with
        | :? ChatCompletionsFunctionToolCall as fnCall -> Some fnCall
        | _ -> None

    let private tryGetFirstFunctionToolCallName (assistant: ChatRequestAssistantMessage) =
        assistant.ToolCalls
        |> Seq.tryHead
        |> Option.bind (function | FunctionToolCall fnCall -> Some fnCall.Name | _ -> None)

    [<Tests>]
    let sequentialToolWorkflowTests =
        testList "Multi-Turn Sequential Tool Execution Expecto Spec" [

            testAsync "SPEC: Multi-turn sequential tool call workflow (LLM -> Tool 1 -> LLM -> Tool 2 -> LLM -> Text)" {
                let callCounter = ref 0

                let mockLlmCaller : LlmCaller =
                    fun _ _ -> async {
                        incr callCounter
                        match !callCounter with
                        | 1 -> return Ok (createToolCallCompletions "call_weather_123" "get_weather" "{\"location\":\"Tokyo\"}")
                        | 2 -> return Ok (createToolCallCompletions "call_contact_456" "search_contacts" "{\"name\":\"Alice\"}")
                        | 3 -> return Ok (createTextCompletions "Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com).")
                        | _ -> return Error "Unexpected LLM call beyond expected sequence"
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
                        ChatRequestSystemMessage("You are a helpful assistant.") :> ChatRequestMessage
                        ChatRequestUserMessage("Find weather in Tokyo and notify Alice.") :> ChatRequestMessage
                    ]
                    ApiCalls = 0
                    EmptyContentRetries = 0
                    InterruptRequested = false
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
                | AssistantMessage asstMsg1, ToolMessage toolMsg1, AssistantMessage asstMsg2, ToolMessage toolMsg2, AssistantMessage asstMsg3 ->
                    let actual = {|
                        FirstToolName = tryGetFirstFunctionToolCallName asstMsg1
                        FirstToolResult = toolMsg1.Content
                        FirstToolCallId = toolMsg1.ToolCallId
                        SecondToolName = tryGetFirstFunctionToolCallName asstMsg2
                        SecondToolResult = toolMsg2.Content
                        SecondToolCallId = toolMsg2.ToolCallId
                        FinalAssistantText = asstMsg3.Content
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
