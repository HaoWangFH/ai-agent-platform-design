namespace Skight.AgentPlatform.FSharp.Tests

open System
open Azure.AI.OpenAI
open Expecto
open Skight.AgentPlatform.FSharp

module AgentPipelineTests =

    let createTestState (messages: ChatRequestMessage list) : TurnState = {
        Messages = messages
        ApiCalls = 0
        EmptyContentRetries = 0
        InterruptRequested = false
        Config = {
            MaxIterations = 5
            MaxRetries = 2
            ContextWindowLimit = 10
            Model = "test-model"
        }
    }

    [<Tests>]
    let pipelineTests =
        testList "Agent Pipeline Pure Step Specification Tests" [

            test "Step 2.1a Interrupt Check returns Exit when interrupt requested" {
                let initialState = createTestState [ ChatRequestSystemMessage("sys") :> ChatRequestMessage ]
                let interruptedState = { initialState with InterruptRequested = true }
                
                match AgentPipeline.checkInterrupt interruptedState with
                | Exit res ->
                    Expect.isTrue res.Interrupted "Expected turn to be interrupted"
                    Expect.equal res.ExitReason Interrupted "Expected Interrupted exit reason"
                | Continue _ ->
                    failwith "Expected step to exit on interrupt"
            }

            test "Step 2.1b Budget Check returns Exit when max iterations reached" {
                let initialState = createTestState [ ChatRequestSystemMessage("sys") :> ChatRequestMessage ]
                let exhaustedState = { initialState with ApiCalls = 5 }
                
                match AgentPipeline.checkBudget exhaustedState with
                | Exit res ->
                    Expect.isTrue res.Failed "Expected turn to be marked failed"
                    Expect.equal res.ExitReason BudgetExhausted "Expected BudgetExhausted exit reason"
                | Continue _ ->
                    failwith "Expected step to exit on budget exhaustion"
            }

            test "Step 2.3 Context Window Protection trims middle history when exceeding limit" {
                let systemMsg = ChatRequestSystemMessage("system prompt") :> ChatRequestMessage
                let history = systemMsg :: ([ 1 .. 15 ] |> List.map (fun i -> ChatRequestUserMessage(sprintf "msg %d" i) :> ChatRequestMessage))
                
                let compressed = AgentPipeline.compressContextIfNeeded 10 history
                
                Expect.isTrue (compressed.Length <= 10) "Compressed history should be within limit"
                Expect.equal compressed.Head systemMsg "System prompt should be preserved as head"
            }

            test "Step 2.7 Process Text Response finalizes turn with clean text" {
                let initialState = createTestState [ ChatRequestSystemMessage("sys") :> ChatRequestMessage; ChatRequestUserMessage("Hi") :> ChatRequestMessage ]
                
                match AgentPipeline.processTextResponse "Hello world!" initialState with
                | Exit res ->
                    Expect.isTrue res.Completed "Expected turn to complete"
                    Expect.equal res.FinalResponse "Hello world!" "Expected matching final text"
                    Expect.equal res.ExitReason (TextResponse "Hello world!") "Expected TextResponse exit reason"
                | Continue _ ->
                    failwith "Expected text response to finalize turn"
            }

            test "Step 2.7 Empty Response Recovery nudges prompt on empty content" {
                let initialState = createTestState [ ChatRequestSystemMessage("sys") :> ChatRequestMessage; ChatRequestUserMessage("Hi") :> ChatRequestMessage ]
                
                match AgentPipeline.processTextResponse "" initialState with
                | Continue nextState ->
                    Expect.equal nextState.EmptyContentRetries 1 "Expected EmptyContentRetries count to increment"
                    Expect.equal nextState.Messages.Length 3 "Expected 3 messages in state after prompt nudge"
                | Exit _ ->
                    failwith "Expected empty content to trigger retry prompt nudge"
            }

            testAsync "Functional Loop handles LlmCaller error gracefully" {
                async {
                    let dummyLlmCaller : LlmCaller =
                        fun _ _ -> async { return Error "API Connection Failed" }

                    let dummyExecutor : ToolExecutor =
                        fun _ _ -> async { return "tool output" }

                    let initialState = createTestState [ ChatRequestSystemMessage("sys") :> ChatRequestMessage; ChatRequestUserMessage("test") :> ChatRequestMessage ]

                    let! result = AgentPipeline.runTurnLoop dummyLlmCaller dummyExecutor [] Set.empty initialState

                    Expect.isTrue result.Failed "Expected turn to fail on API error"
                    Expect.equal result.Error (Some "API Connection Failed") "Expected matching error string"
                    Expect.equal result.ExitReason (ApiError "API Connection Failed") "Expected ApiError exit reason"
                    return ()
                }
            }
        ]
