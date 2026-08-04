namespace Skight.AgentPlatform.FSharp

open System
open System.Collections.Generic
open System.Text.Json
open Azure.AI.OpenAI

type BearerTokenPolicy(token: string) =
    inherit Azure.Core.Pipeline.HttpPipelineSynchronousPolicy()
    override _.OnSendingRequest(message: Azure.Core.HttpMessage) =
        message.Request.Headers.SetValue("Authorization", sprintf "Bearer %s" token)

module AgentPipeline =

    /// Step 2.1a: Pre-API Check - Interrupt Guard
    let checkInterrupt (state: TurnState) : StepResult<TurnState, TurnResult> =
        if state.InterruptRequested then
            printfn "  [Turn Exit] Turn interrupted by user."
            Exit {
                Outcome = TurnOutcome.Interrupted ExitReason.Interrupted
                Messages = state.Messages
                ApiCalls = state.ApiCalls
            }
        else
            Continue state

    /// Step 2.1b: Pre-API Check - Budget Guard
    let checkBudget (state: TurnState) : StepResult<TurnState, TurnResult> =
        if state.ApiCalls >= state.Config.MaxIterations then
            printfn "  [Turn Exit] Reached max iterations (%d)." state.Config.MaxIterations
            Exit {
                Outcome = TurnOutcome.Failed (ExitReason.BudgetExhausted, Some "Budget exhausted")
                Messages = state.Messages
                ApiCalls = state.ApiCalls
            }
        else
            Continue { state with ApiCalls = state.ApiCalls + 1 }

    /// Step 2.2: Prepare Messages Payload (shallow copy pipeline step)
    let prepareApiMessages (msgs: ChatRequestMessage list) : ChatRequestMessage list =
        msgs |> List.map id

    /// Step 2.3: Context Window Protection (Pure Pipeline Transformation)
    let compressContextIfNeeded (limit: int) (msgs: ChatRequestMessage list) : ChatRequestMessage list =
        if msgs.Length <= limit then
            msgs
        else
            printfn "  [Context Window Protection] History size (%d) > limit (%d). Trimming middle history..." msgs.Length limit
            let systemPrompt = msgs.Head
            let recentCount = limit - 3
            let recentMessages = 
                msgs 
                |> List.skip (msgs.Length - recentCount)
                |> List.skipWhile (function :? ChatRequestToolMessage -> true | _ -> false)

            let summaryMsg = ChatRequestSystemMessage(
                sprintf "[System: Previous conversation history was trimmed to fit context window. %d earlier messages summarized.]" (msgs.Length - recentMessages.Length - 1)
            )
            systemPrompt :: (summaryMsg :> ChatRequestMessage) :: recentMessages

    /// Pipeline composition for message payload preparation
    let preparePayload (limit: int) (msgs: ChatRequestMessage list) : ChatRequestMessage list =
        msgs 
        |> prepareApiMessages 
        |> compressContextIfNeeded limit

    /// Step 2.4: Execute API Call with Exponential Backoff Retry (Pure Async Recursion)
    let rec callLlmWithRetry (llmCaller: LlmCaller) (schemas: FunctionDefinition list) (maxRetries: int) (retryCount: int) (msgs: ChatRequestMessage list) : Async<Result<ChatCompletions, string>> =
        async {
            let! result = llmCaller schemas msgs
            match result with
            | Ok completions -> return Ok completions
            | Error err ->
                printfn "  [API Error Retry %d/%d] %s" (retryCount + 1) maxRetries err
                if retryCount >= maxRetries - 1 then
                    return Error err
                else
                    let delayMs = (int (Math.Pow(2.0, float retryCount))) * 1000
                    do! Async.Sleep delayMs
                    return! callLlmWithRetry llmCaller schemas maxRetries (retryCount + 1) msgs
        }

    /// Step 2.6: Process Tool Calls (Self-Correction, JSON Validation & Execution)
    let processToolCalls (executor: ToolExecutor) (registeredNamesSet: Set<string>) (content: string) (toolCalls: IEnumerable<ChatCompletionsToolCall>) (state: TurnState) : Async<TurnState> =
        async {
            let assistantMsg = ChatRequestAssistantMessage(content)

            let! newToolMessages = 
                toolCalls
                |> Seq.choose (fun (tc: ChatCompletionsToolCall) ->
                    match tc with
                    | :? ChatCompletionsFunctionToolCall as fnCall -> Some fnCall
                    | _ -> None)
                |> Seq.map (fun fnCall ->
                    async {
                        assistantMsg.ToolCalls.Add(fnCall)
                        let name = fnCall.Name
                        let callId = fnCall.Id
                        let argsStr = fnCall.Arguments

                        if not (registeredNamesSet.Contains(name)) then
                            let avail = registeredNamesSet |> String.concat ", "
                            let errStr = sprintf "Error: Tool '%s' is not registered. Available tools: %s" name avail
                            printfn "  [Tool Validation Error] %s" errStr
                            return ChatRequestToolMessage(errStr, callId) :> ChatRequestMessage
                        else
                            try
                                use doc = JsonDocument.Parse(if String.IsNullOrEmpty argsStr then "{}" else argsStr)
                                printfn "  [Tool Execution] %s(%s)" name argsStr
                                let! execResult = executor name argsStr
                                printfn "  [Tool Result] %s" execResult
                                return ChatRequestToolMessage(execResult, callId) :> ChatRequestMessage
                            with jsonEx ->
                                let errStr = sprintf "Error: Invalid JSON arguments for tool '%s': %s" name jsonEx.Message
                                printfn "  [JSON Parse Error] %s" errStr
                                return ChatRequestToolMessage(errStr, callId) :> ChatRequestMessage
                    })
                |> Async.Parallel

            let updatedHistory = 
                state.Messages 
                @ (assistantMsg :> ChatRequestMessage :: (newToolMessages |> Array.toList))

            return { state with Messages = updatedHistory }
        }

    /// Step 2.7: Process Final Text Response with Empty Response Recovery
    let processTextResponse (rawContent: string) (state: TurnState) : StepResult<TurnState, TurnResult> =
        let finalText = if isNull rawContent then "" else rawContent.Trim()
        
        if String.IsNullOrEmpty finalText then
            if state.EmptyContentRetries < 2 then
                printfn "  [Empty Response Recovery] Retrying with prompt nudge..."
                let updatedHistory = state.Messages @ [ ChatRequestUserMessage("Please provide a complete text response summarizing your answer.") :> ChatRequestMessage ]
                Continue { state with Messages = updatedHistory; EmptyContentRetries = state.EmptyContentRetries + 1 }
            else
                let fallbackText = "(empty response)"
                printfn "Assistant: %s" fallbackText
                let updatedHistory = state.Messages @ [ ChatRequestAssistantMessage(fallbackText) :> ChatRequestMessage ]
                Exit {
                    Outcome = TurnOutcome.Completed fallbackText
                    Messages = updatedHistory
                    ApiCalls = state.ApiCalls
                }
        else
            printfn "Assistant: %s" finalText
            let updatedHistory = state.Messages @ [ ChatRequestAssistantMessage(finalText) :> ChatRequestMessage ]
            Exit {
                Outcome = TurnOutcome.Completed finalText
                Messages = updatedHistory
                ApiCalls = state.ApiCalls
            }

    /// Pure, Tail-Recursive 4-Phase Turn Loop Function
    let rec runTurnLoop (llmCaller: LlmCaller) (executor: ToolExecutor) (registeredSchemas: FunctionDefinition list) (registeredNamesSet: Set<string>) (state: TurnState) : Async<TurnResult> =
        async {
            // Pipeline Step 2.1: Pre-API Checks (Interrupt Guard -> Budget Guard)
            match checkInterrupt state with
            | Exit result -> return result
            | Continue stateAfterInterruptCheck ->

            match checkBudget stateAfterInterruptCheck with
            | Exit result -> return result
            | Continue stateAfterBudgetCheck ->

            // Pipeline Step 2.2 & 2.3: Message Payload Preparation & Context Compression
            let preparedPayload = preparePayload stateAfterBudgetCheck.Config.ContextWindowLimit stateAfterBudgetCheck.Messages

            // Pipeline Step 2.4: Inner LLM API Call with Retry
            let! apiResult = callLlmWithRetry llmCaller registeredSchemas stateAfterBudgetCheck.Config.MaxRetries 0 preparedPayload

            match apiResult with
            | Error err ->
                return {
                    Outcome = TurnOutcome.Failed (ExitReason.ApiError err, Some err)
                    Messages = stateAfterBudgetCheck.Messages
                    ApiCalls = stateAfterBudgetCheck.ApiCalls
                }
            | Ok completions when completions.Choices.Count = 0 ->
                return {
                    Outcome = TurnOutcome.Failed (ExitReason.NoResponse "No choices returned", Some "No choices returned from LLM")
                    Messages = stateAfterBudgetCheck.Messages
                    ApiCalls = stateAfterBudgetCheck.ApiCalls
                }
            | Ok completions ->
                let choice = completions.Choices.[0]
                let message = choice.Message

                // Pipeline Step 2.6: Tool Call Execution Path
                if not (isNull message.ToolCalls) && message.ToolCalls.Count > 0 then
                    let! nextState = processToolCalls executor registeredNamesSet message.Content message.ToolCalls stateAfterBudgetCheck
                    return! runTurnLoop llmCaller executor registeredSchemas registeredNamesSet nextState

                // Pipeline Step 2.7: Final Text Response Path
                else
                    match processTextResponse message.Content stateAfterBudgetCheck with
                    | Exit turnResult -> return turnResult
                    | Continue nextState -> return! runTurnLoop llmCaller executor registeredSchemas registeredNamesSet nextState
        }

/// Agent class wrapper around the pure functional AgentPipeline
type Agent(apiKey: string, registry: ToolRegistry, config: AgentConfig, ?endpoint: string, ?jwtToken: string) =
    let options = OpenAIClientOptions()
    do
        match jwtToken with
        | Some t when not (String.IsNullOrEmpty t) ->
            options.AddPolicy(BearerTokenPolicy(t), Azure.Core.HttpPipelinePosition.PerCall)
        | _ -> ()

    let client =
        match endpoint with
        | Some ep when not (String.IsNullOrEmpty ep) ->
            OpenAIClient(Uri(ep), Azure.AzureKeyCredential(apiKey), options)
        | _ ->
            OpenAIClient(apiKey, options)

    // Standard default Azure.AI.OpenAI LLM caller implementation
    let defaultLlmCaller : LlmCaller =
        fun schemas msgs ->
            async {
                let reqOptions = ChatCompletionsOptions(config.Model, msgs)
                reqOptions.Temperature <- Nullable(0.7f)
                for schema in schemas do
                    reqOptions.Tools.Add(ChatCompletionsFunctionToolDefinition(schema))
                try
                    let! resp = client.GetChatCompletionsAsync(reqOptions) |> Async.AwaitTask
                    return Ok resp.Value
                with ex ->
                    return Error ex.Message
            }

    let mutable canonicalMessages : ChatRequestMessage list = []
    let mutable interruptRequested = false

    do
        let systemPrompt =
            "You are a helpful AI assistant. You have access to various tools. " +
            "When asked to perform a task, use the tools to gather information and take actions before answering."
        canonicalMessages <- [ ChatRequestSystemMessage(systemPrompt) :> ChatRequestMessage ]

    member _.RequestInterrupt() =
        interruptRequested <- true

    /// Executes a turn using the composable functional loop pipeline
    member _.RunAsync(userInput: string, ?customLlmCaller: LlmCaller, ?customExecutor: ToolExecutor) : Async<TurnResult> =
        async {
            // Phase 1: Turn Prologue
            printfn "\nUser: %s" userInput
            canonicalMessages <- canonicalMessages @ [ ChatRequestUserMessage(userInput) :> ChatRequestMessage ]

            let initialState : TurnState = {
                Messages = canonicalMessages
                ApiCalls = 0
                EmptyContentRetries = 0
                InterruptRequested = interruptRequested
                Config = config
            }

            interruptRequested <- false

            let activeLlmCaller = defaultArg customLlmCaller defaultLlmCaller
            let activeExecutor = defaultArg customExecutor registry.AsExecutor
            let registeredSchemas = registry.GetToolSchemas()
            let registeredNamesSet = registry.GetRegisteredNames() |> Set.ofList

            // Run pure, tail-recursive 4-phase functional loop
            let! result = AgentPipeline.runTurnLoop activeLlmCaller activeExecutor registeredSchemas registeredNamesSet initialState
            
            // Sync canonical state
            canonicalMessages <- result.Messages
            return result
        }
