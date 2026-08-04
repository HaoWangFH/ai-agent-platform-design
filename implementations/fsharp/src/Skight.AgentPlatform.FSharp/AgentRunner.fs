namespace Skight.AgentPlatform.FSharp

module AgentRunner =

    /// Pure functional entry point: (State, Input) -> Async<TurnResult * AgentSessionState>
    let runTurnAsync 
        (llmCaller: LlmCaller) 
        (executor: ToolExecutor) 
        (config: AgentConfig) 
        (userInput: string) 
        (sessionState: AgentSessionState) 
        (registeredSchemas: ToolSchema list) 
        (registeredNamesSet: Set<ToolName>) : Async<TurnResult * AgentSessionState> =
        
        async {
            // Phase 1: Turn Prologue
            printfn "\nUser: %s" userInput
            let initialState, nextSessionState = AgentSession.beginTurn config userInput sessionState
            
            // Run pure, tail-recursive 4-phase functional loop
            let! result = AgentPipeline.runTurnLoop llmCaller executor registeredSchemas registeredNamesSet initialState
            
            // Phase 5: State sync
            let finalSessionState = AgentSession.applyTurnResult result nextSessionState
            
            return (result, finalSessionState)
        }
