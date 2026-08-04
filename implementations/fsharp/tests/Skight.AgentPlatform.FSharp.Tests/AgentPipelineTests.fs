namespace Skight.AgentPlatform.FSharp.Tests

open Expecto
open Skight.AgentPlatform.FSharp

module AgentPipelineTests =

    let createTestState (messages: AgentMessage list) : TurnState = {
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
                let initialState = createTestState [ SystemMessage "sys" ]
                let interruptedState = { initialState with InterruptRequested = true }

                match AgentPipeline.checkInterrupt interruptedState with
                | Exit { Outcome = TurnOutcome.Interrupted reason; ApiCalls = apiCalls } ->
                    let actual = {| Reason = reason; ApiCalls = apiCalls |}
                    let expected = {| Reason = ExitReason.Interrupted; ApiCalls = 0 |}
                    Expect.equal actual expected "Expected Interrupted outcome"
                | Exit result ->
                    failtestf "Expected interrupted exit, got %A" result.Outcome
                | Continue _ ->
                    failtest "Expected step to exit on interrupt"
            }

            test "Step 2.1b Budget Check returns Exit when max iterations reached" {
                let initialState = createTestState [ SystemMessage "sys" ]
                let exhaustedState = { initialState with ApiCalls = 5 }

                match AgentPipeline.checkBudget exhaustedState with
                | Exit { Outcome = TurnOutcome.Failed (reason, error) } ->
                    let actual = {| Reason = reason; Error = error |}
                    let expected = {| Reason = ExitReason.BudgetExhausted; Error = Some "Budget exhausted" |}
                    Expect.equal actual expected "Expected budget failure outcome"
                | Exit result ->
                    failtestf "Expected failed exit, got %A" result.Outcome
                | Continue _ ->
                    failtest "Expected step to exit on budget exhaustion"
            }

            test "Step 2.3 Context Window Protection trims middle history when exceeding limit" {
                let systemMsg = SystemMessage "system prompt"
                let history = systemMsg :: ([ 1 .. 15 ] |> List.map (fun i -> UserMessage(sprintf "msg %d" i)))

                let compressed = AgentPipeline.compressContextIfNeeded 10 history
                let actual = {| IsWithinLimit = compressed.Length <= 10; Head = compressed.Head |}
                let expected = {| IsWithinLimit = true; Head = systemMsg |}
                Expect.equal actual expected "Compressed history should keep system prompt and stay within limit"
            }

            test "Step 2.7 Process Text Response finalizes turn with clean text" {
                let initialState = createTestState [ SystemMessage "sys"; UserMessage "Hi" ]

                match AgentPipeline.processTextResponse "Hello world!" initialState with
                | Exit { Outcome = TurnOutcome.Completed finalText } ->
                    Expect.equal finalText "Hello world!" "Expected matching final text"
                | Exit result ->
                    failtestf "Expected completed exit, got %A" result.Outcome
                | Continue _ ->
                    failtest "Expected text response to finalize turn"
            }

            test "Step 2.7 Empty Response Recovery nudges prompt on empty content" {
                let initialState = createTestState [ SystemMessage "sys"; UserMessage "Hi" ]

                match AgentPipeline.processTextResponse "" initialState with
                | Continue nextState ->
                    let actual = {| EmptyContentRetries = nextState.EmptyContentRetries; MessageCount = nextState.Messages.Length |}
                    let expected = {| EmptyContentRetries = 1; MessageCount = 3 |}
                    Expect.equal actual expected "Expected prompt nudge retry state"
                | Exit result ->
                    failtestf "Expected Continue for empty content, got %A" result.Outcome
            }

            testAsync "Functional Loop handles LlmCaller error gracefully" {
                let dummyLlmCaller : LlmCaller =
                    fun _ _ -> async { return Error "API Connection Failed" }

                let dummyExecutor : ToolExecutor =
                    fun _ _ -> async { return "tool output" }

                let initialState = createTestState [ SystemMessage "sys"; UserMessage "test" ]

                let! result = AgentPipeline.runTurnLoop dummyLlmCaller dummyExecutor [] Set.empty initialState

                match result.Outcome with
                | TurnOutcome.Failed (reason, error) ->
                    let actual = {| Reason = reason; Error = error |}
                    let expected = {| Reason = ExitReason.ApiError "API Connection Failed"; Error = Some "API Connection Failed" |}
                    Expect.equal actual expected "Expected API error outcome"
                | outcome ->
                    failtestf "Expected failed outcome, got %A" outcome
            }
        ]
