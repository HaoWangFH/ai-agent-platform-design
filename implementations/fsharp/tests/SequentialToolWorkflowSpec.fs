namespace AgentPlatformFSharp.Tests

open Xunit
open System
open Azure.AI.OpenAI
open AgentPlatform.FSharp

/// <summary>
/// Specification-Driven Test Suite: Multi-Turn Sequential Tool Execution Loop
/// 
/// SPECIFICATION:
/// Given a user request requiring multiple sequential tools ("get_weather" -> "search_contacts")
/// And a mock LLM configured to return dependent tool calls across turns
/// When the turn loop runs
/// Then the agent must iteratively execute tools, update state, and return the final text response.
/// </summary>
module SequentialToolWorkflowSpec =

    let private createToolCallCompletions (toolCallId: string) (toolName: string) (argumentsJson: string) =
        let toolCall = ChatCompletionsFunctionToolCall(toolCallId, toolName, argumentsJson)
        let responseMsg = AzureOpenAIModelFactory.ChatResponseMessage(ChatRole.Assistant, "", [ toolCall ])
        let choice = AzureOpenAIModelFactory.ChatChoice(responseMsg, 0, Nullable(CompletionsFinishReason.ToolCalls), null)
        AzureOpenAIModelFactory.ChatCompletions(null, DateTimeOffset.UtcNow, [ choice ], null, null, null)

    let private createTextCompletions (textResponse: string) =
        let responseMsg = AzureOpenAIModelFactory.ChatResponseMessage(ChatRole.Assistant, textResponse, null)
        let choice = AzureOpenAIModelFactory.ChatChoice(responseMsg, 0, Nullable(CompletionsFinishReason.Stopped), null)
        AzureOpenAIModelFactory.ChatCompletions(null, DateTimeOffset.UtcNow, [ choice ], null, null, null)

    [<Fact>]
    let ``SPEC: Multi-turn sequential tool call workflow (LLM -> Tool 1 -> LLM -> Tool 2 -> LLM -> Text)`` () =
        // =========================================================================================
        // GIVEN: A mock LLM configured for 3 sequential iterations:
        //   - Iteration 1: Request get_weather("Tokyo")
        //   - Iteration 2: Request search_contacts("Alice")
        //   - Iteration 3: Return final summary text
        // =========================================================================================
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

        // GIVEN: Registered tool handlers
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

        // =========================================================================================
        // WHEN: Executing the pure tail-recursive turn loop
        // =========================================================================================
        let result = AgentPipeline.runTurnLoop mockLlmCaller mockExecutor [] registeredNamesSet initialState |> Async.RunSynchronously

        // =========================================================================================
        // THEN: Verify full turn execution parity and message history sequence
        // =========================================================================================
        Assert.True(result.Completed, "Turn should complete successfully")
        Assert.False(result.Failed, "Turn should not fail")
        Assert.Equal(3, result.ApiCalls)
        Assert.Equal("Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com).", result.FinalResponse)
        
        // Assert exact 7 messages in state:
        // [1] System, [2] User, [3] Asst(Tool1), [4] Tool(Res1), [5] Asst(Tool2), [6] Tool(Res2), [7] Asst(FinalText)
        Assert.Equal(7, result.Messages.Length)
        
        // Verify Message 3 & 4 (First tool cycle)
        let asstMsg1 = result.Messages.[2] :?> ChatRequestAssistantMessage
        Assert.NotNull(asstMsg1.ToolCalls)
        Assert.Equal("get_weather", (asstMsg1.ToolCalls.[0] :?> ChatCompletionsFunctionToolCall).Name)

        let toolMsg1 = result.Messages.[3] :?> ChatRequestToolMessage
        Assert.Equal("25°C, Sunny", toolMsg1.Content)
        Assert.Equal("call_weather_123", toolMsg1.ToolCallId)

        // Verify Message 5 & 6 (Second tool cycle)
        let asstMsg2 = result.Messages.[4] :?> ChatRequestAssistantMessage
        Assert.NotNull(asstMsg2.ToolCalls)
        Assert.Equal("search_contacts", (asstMsg2.ToolCalls.[0] :?> ChatCompletionsFunctionToolCall).Name)

        let toolMsg2 = result.Messages.[5] :?> ChatRequestToolMessage
        Assert.Equal("alice@example.com", toolMsg2.Content)
        Assert.Equal("call_contact_456", toolMsg2.ToolCallId)

        // Verify Message 7 (Final text response)
        let asstMsg3 = result.Messages.[6] :?> ChatRequestAssistantMessage
        Assert.Equal("Successfully retrieved Tokyo weather (25°C, Sunny) and emailed Alice (alice@example.com).", asstMsg3.Content)
