namespace AgentPlatformFSharp.Tests

open Xunit
open System
open Azure.AI.OpenAI
open AgentPlatform.FSharp

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

    [<Fact>]
    let ``Step 2.1a Interrupt Check returns Exit when interrupt requested`` () =
        let initialState = createTestState [ ChatRequestSystemMessage("sys") :> ChatRequestMessage ]
        let interruptedState = { initialState with InterruptRequested = true }
        
        match AgentPipeline.checkInterrupt interruptedState with
        | Exit res ->
            Assert.True(res.Interrupted)
            Assert.Equal(Interrupted, res.ExitReason)
        | Continue _ ->
            Assert.Fail("Expected step to exit on interrupt")

    [<Fact>]
    let ``Step 2.1b Budget Check returns Exit when max iterations reached`` () =
        let initialState = createTestState [ ChatRequestSystemMessage("sys") :> ChatRequestMessage ]
        let exhaustedState = { initialState with ApiCalls = 5 }
        
        match AgentPipeline.checkBudget exhaustedState with
        | Exit res ->
            Assert.True(res.Failed)
            Assert.Equal(BudgetExhausted, res.ExitReason)
        | Continue _ ->
            Assert.Fail("Expected step to exit on budget exhaustion")

    [<Fact>]
    let ``Step 2.3 Context Window Protection trims middle history when exceeding limit`` () =
        let systemMsg = ChatRequestSystemMessage("system prompt") :> ChatRequestMessage
        let history = systemMsg :: ([ 1 .. 15 ] |> List.map (fun i -> ChatRequestUserMessage(sprintf "msg %d" i) :> ChatRequestMessage))
        
        let compressed = AgentPipeline.compressContextIfNeeded 10 history
        
        Assert.True(compressed.Length <= 10, "Compressed history should be within limit")
        Assert.Same(systemMsg, compressed.Head)

    [<Fact>]
    let ``Step 2.7 Process Text Response finalizes turn with clean text`` () =
        let initialState = createTestState [ ChatRequestSystemMessage("sys") :> ChatRequestMessage; ChatRequestUserMessage("Hi") :> ChatRequestMessage ]
        
        match AgentPipeline.processTextResponse "Hello world!" initialState with
        | Exit res ->
            Assert.True(res.Completed)
            Assert.Equal("Hello world!", res.FinalResponse)
            Assert.Equal(TextResponse "Hello world!", res.ExitReason)
        | Continue _ ->
            Assert.Fail("Expected text response to finalize turn")

    [<Fact>]
    let ``Step 2.7 Empty Response Recovery nudges prompt on empty content`` () =
        let initialState = createTestState [ ChatRequestSystemMessage("sys") :> ChatRequestMessage; ChatRequestUserMessage("Hi") :> ChatRequestMessage ]
        
        match AgentPipeline.processTextResponse "" initialState with
        | Continue nextState ->
            Assert.Equal(1, nextState.EmptyContentRetries)
            Assert.Equal(3, nextState.Messages.Length) // system + user + nudge user msg
        | Exit _ ->
            Assert.Fail("Expected empty content to trigger retry prompt nudge")

    [<Fact>]
    let ``Functional Loop handles LlmCaller error gracefully`` () =
        let dummyLlmCaller : LlmCaller =
            fun _ _ -> async { return Error "API Connection Failed" }

        let dummyExecutor : ToolExecutor =
            fun _ _ -> async { return "tool output" }

        let initialState = createTestState [ ChatRequestSystemMessage("sys") :> ChatRequestMessage; ChatRequestUserMessage("test") :> ChatRequestMessage ]

        let result = AgentPipeline.runTurnLoop dummyLlmCaller dummyExecutor [] Set.empty initialState |> Async.RunSynchronously

        Assert.True(result.Failed)
        Assert.Equal(Some "API Connection Failed", result.Error)
        Assert.Equal(ApiError "API Connection Failed", result.ExitReason)
