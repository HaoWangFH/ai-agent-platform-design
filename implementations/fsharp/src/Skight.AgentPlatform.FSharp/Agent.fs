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
        match state.Command with
        | InterruptTurn ->
            printfn "  [Turn Exit] Turn interrupted by user."
            Exit {
                Outcome = TurnOutcome.Interrupted ExitReason.Interrupted
                Messages = state.Messages
                ApiCalls = state.ApiCalls
            }
        | RunTurn ->
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
    let prepareApiMessages (msgs: AgentMessage list) : AgentMessage list =
        msgs |> List.map id

    /// Step 2.3: Context Window Protection (Pure Pipeline Transformation)
    let compressContextIfNeeded (limit: int) (msgs: AgentMessage list) : AgentMessage list =
        if msgs.Length <= limit then
            msgs
        else
            printfn "  [Context Window Protection] History size (%d) > limit (%d). Trimming middle history..." msgs.Length limit
            let systemPrompt = msgs.Head
            let recentCount = limit - 3
            let recentMessages =
                msgs
                |> List.skip (msgs.Length - recentCount)
                |> List.skipWhile (function | ToolMessage _ -> true | _ -> false)

            let summaryMsg =
                SystemMessage (
                    sprintf "[System: Previous conversation history was trimmed to fit context window. %d earlier messages summarized.]" (msgs.Length - recentMessages.Length - 1)
                )

            systemPrompt :: summaryMsg :: recentMessages

    /// Pipeline composition for message payload preparation
    let preparePayload (limit: int) (msgs: AgentMessage list) : AgentMessage list =
        msgs
        |> prepareApiMessages
        |> compressContextIfNeeded limit

    /// Step 2.4: Execute API Call with Exponential Backoff Retry (Pure Async Recursion)
    let rec callLlmWithRetry (llmCaller: LlmCaller) (schemas: ToolSchema list) (maxRetries: int) (retryCount: int) (msgs: AgentMessage list) : Async<Result<LlmTurnResponse, string>> =
        async {
            let! result = llmCaller schemas msgs
            match result with
            | Ok response -> return Ok response
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
    let processToolCalls (executor: ToolExecutor) (registeredNamesSet: Set<string>) (content: string) (toolCalls: ToolCall list) (state: TurnState) : Async<TurnState> =
        async {
            let! newToolMessages =
                toolCalls
                |> Seq.map (fun toolCall ->
                    async {
                        let name = toolCall.Name
                        let callId = toolCall.Id
                        let argsStr = toolCall.ArgumentsJson

                        if not (registeredNamesSet.Contains(name)) then
                            let avail = registeredNamesSet |> String.concat ", "
                            let errStr = sprintf "Error: Tool '%s' is not registered. Available tools: %s" name avail
                            printfn "  [Tool Validation Error] %s" errStr
                            return ToolMessage(callId, errStr)
                        else
                            try
                                use doc = JsonDocument.Parse(if String.IsNullOrEmpty argsStr then "{}" else argsStr)
                                printfn "  [Tool Execution] %s(%s)" name argsStr
                                let! execResult = executor name argsStr
                                printfn "  [Tool Result] %s" execResult
                                return ToolMessage(callId, execResult)
                            with jsonEx ->
                                let errStr = sprintf "Error: Invalid JSON arguments for tool '%s': %s" name jsonEx.Message
                                printfn "  [JSON Parse Error] %s" errStr
                                return ToolMessage(callId, errStr)
                    })
                |> Async.Parallel

            let assistantMsg = AssistantMessage(content, toolCalls)
            let updatedHistory = state.Messages @ (assistantMsg :: (newToolMessages |> Array.toList))
            return { state with Messages = updatedHistory }
        }

    /// Step 2.7: Process Final Text Response with Empty Response Recovery
    let processTextResponse (rawContent: string) (state: TurnState) : StepResult<TurnState, TurnResult> =
        let finalText = if isNull rawContent then "" else rawContent.Trim()

        if String.IsNullOrEmpty finalText then
            if state.EmptyContentRetries < 2 then
                printfn "  [Empty Response Recovery] Retrying with prompt nudge..."
                let updatedHistory = state.Messages @ [ UserMessage "Please provide a complete text response summarizing your answer." ]
                Continue { state with Messages = updatedHistory; EmptyContentRetries = state.EmptyContentRetries + 1 }
            else
                let fallbackText = "(empty response)"
                printfn "Assistant: %s" fallbackText
                let updatedHistory = state.Messages @ [ AssistantMessage(fallbackText, []) ]
                Exit {
                    Outcome = TurnOutcome.Completed fallbackText
                    Messages = updatedHistory
                    ApiCalls = state.ApiCalls
                }
        else
            printfn "Assistant: %s" finalText
            let updatedHistory = state.Messages @ [ AssistantMessage(finalText, []) ]
            Exit {
                Outcome = TurnOutcome.Completed finalText
                Messages = updatedHistory
                ApiCalls = state.ApiCalls
            }

    /// Pure, Tail-Recursive 4-Phase Turn Loop Function
    let rec runTurnLoop (llmCaller: LlmCaller) (executor: ToolExecutor) (registeredSchemas: ToolSchema list) (registeredNamesSet: Set<string>) (state: TurnState) : Async<TurnResult> =
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
            | Error err when err = "No choices returned from LLM" ->
                return {
                    Outcome = TurnOutcome.Failed (ExitReason.NoResponse "No choices returned", Some err)
                    Messages = stateAfterBudgetCheck.Messages
                    ApiCalls = stateAfterBudgetCheck.ApiCalls
                }
            | Error err ->
                return {
                    Outcome = TurnOutcome.Failed (ExitReason.ApiError err, Some err)
                    Messages = stateAfterBudgetCheck.Messages
                    ApiCalls = stateAfterBudgetCheck.ApiCalls
                }
            | Ok response ->
                // Pipeline Step 2.6: Tool Call Execution Path
                if response.ToolCalls.Length > 0 then
                    let! nextState = processToolCalls executor registeredNamesSet response.Content response.ToolCalls stateAfterBudgetCheck
                    return! runTurnLoop llmCaller executor registeredSchemas registeredNamesSet nextState

                // Pipeline Step 2.7: Final Text Response Path
                else
                    match processTextResponse response.Content stateAfterBudgetCheck with
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

    let toChatRequestMessage (msg: AgentMessage) : ChatRequestMessage =
        match msg with
        | SystemMessage content -> ChatRequestSystemMessage(content) :> ChatRequestMessage
        | UserMessage content -> ChatRequestUserMessage(content) :> ChatRequestMessage
        | AssistantMessage (content, toolCalls) ->
            let assistant = ChatRequestAssistantMessage(content)
            for toolCall in toolCalls do
                assistant.ToolCalls.Add(ChatCompletionsFunctionToolCall(toolCall.Id, toolCall.Name, toolCall.ArgumentsJson))
            assistant :> ChatRequestMessage
        | ToolMessage (toolCallId, content) -> ChatRequestToolMessage(content, toolCallId) :> ChatRequestMessage

    let toDomainResponse (responseMessage: ChatResponseMessage) : LlmTurnResponse =
        let content = if isNull responseMessage.Content then "" else responseMessage.Content
        let toolCalls =
            if isNull responseMessage.ToolCalls then
                []
            else
                responseMessage.ToolCalls
                |> Seq.choose (fun tc ->
                    match tc with
                    | :? ChatCompletionsFunctionToolCall as fnCall ->
                        Some {
                            Id = fnCall.Id
                            Name = fnCall.Name
                            ArgumentsJson = fnCall.Arguments
                        }
                    | _ -> None)
                |> Seq.toList

        {
            Content = content
            ToolCalls = toolCalls
        }

    let toFunctionDefinition (schema: ToolSchema) : FunctionDefinition =
        FunctionDefinition(
            Name = schema.Name,
            Description = schema.Description,
            Parameters = BinaryData.FromString(schema.ParametersJson)
        )

    // Standard default Azure.AI.OpenAI LLM caller implementation
    let defaultLlmCaller : LlmCaller =
        fun schemas msgs ->
            async {
                let requestMessages = msgs |> List.map toChatRequestMessage
                let reqOptions = ChatCompletionsOptions(config.Model, requestMessages)
                reqOptions.Temperature <- Nullable(0.7f)
                for schema in schemas do
                    reqOptions.Tools.Add(ChatCompletionsFunctionToolDefinition(toFunctionDefinition schema))
                try
                    let! resp = client.GetChatCompletionsAsync(reqOptions) |> Async.AwaitTask
                    let completions = resp.Value
                    if completions.Choices.Count = 0 then
                        return Error "No choices returned from LLM"
                    else
                        return Ok (toDomainResponse completions.Choices.[0].Message)
                with ex ->
                    return Error ex.Message
            }

    let beginTurn (config: AgentConfig) (userInput: string) (session: AgentSessionState) : TurnState * AgentSessionState =
        let updatedMessages = session.Messages @ [ UserMessage userInput ]
        let turnState = {
            Messages = updatedMessages
            ApiCalls = 0
            EmptyContentRetries = 0
            Command = session.PendingCommand
            Config = config
        }

        let nextSession = {
            Messages = updatedMessages
            PendingCommand = RunTurn
        }

        turnState, nextSession

    let applyTurnResult (result: TurnResult) (session: AgentSessionState) : AgentSessionState =
        { session with Messages = result.Messages }

    let requestInterrupt (session: AgentSessionState) : AgentSessionState =
        { session with PendingCommand = InterruptTurn }

    let mutable sessionState : AgentSessionState = {
        Messages = []
        PendingCommand = RunTurn
    }

    do
        let systemPrompt =
            "You are a helpful AI assistant. You have access to various tools. " +
            "When asked to perform a task, use the tools to gather information and take actions before answering."
        sessionState <- { sessionState with Messages = [ SystemMessage systemPrompt ] }

    member _.RequestInterrupt() =
        sessionState <- requestInterrupt sessionState

    /// Executes a turn using the composable functional loop pipeline
    member _.RunAsync(userInput: string, ?customLlmCaller: LlmCaller, ?customExecutor: ToolExecutor) : Async<TurnResult> =
        async {
            // Phase 1: Turn Prologue
            printfn "\nUser: %s" userInput
            let initialState, nextSessionState = beginTurn config userInput sessionState
            sessionState <- nextSessionState

            let activeLlmCaller = defaultArg customLlmCaller defaultLlmCaller
            let activeExecutor = defaultArg customExecutor registry.AsExecutor
            let registeredSchemas = registry.GetToolSchemas()
            let registeredNamesSet = registry.GetRegisteredNames() |> Set.ofList

            // Run pure, tail-recursive 4-phase functional loop
            let! result = AgentPipeline.runTurnLoop activeLlmCaller activeExecutor registeredSchemas registeredNamesSet initialState

            // Sync canonical state
            sessionState <- applyTurnResult result sessionState
            return result
        }
