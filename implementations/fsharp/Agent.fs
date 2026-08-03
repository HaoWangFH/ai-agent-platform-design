namespace AgentPlatform.FSharp

open System
open System.Text.Json
open Azure.AI.OpenAI

type BearerTokenPolicy(token: string) =
    inherit Azure.Core.Pipeline.HttpPipelineSynchronousPolicy()
    override _.OnSendingRequest(message: Azure.Core.HttpMessage) =
        message.Request.Headers.SetValue("Authorization", sprintf "Bearer %s" token)

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

    let mutable messages : ChatRequestMessage list = []
    let mutable interruptRequested = false

    do
        let systemPrompt =
            "You are a helpful AI assistant. You have access to various tools. " +
            "When asked to perform a task, use the tools to gather information and take actions before answering."
        messages <- [ ChatRequestSystemMessage(systemPrompt) ]

    member _.RequestInterrupt() =
        interruptRequested <- true

    member private _.PrepareApiMessages(msgs: ChatRequestMessage list) : ChatRequestMessage list =
        // Phase 2.2: Shallow copy list for API request payload
        msgs |> List.map id

    member private _.CompressContextIfNeeded(msgs: ChatRequestMessage list) : ChatRequestMessage list =
        // Phase 2.3: Context window protection using F# pipeline
        if msgs.Length <= config.ContextWindowLimit then
            msgs
        else
            printfn "  [Context Window Protection] History size (%d) > limit (%d). Trimming middle history..." msgs.Length config.ContextWindowLimit
            let systemPrompt = msgs.Head
            let recentCount = config.ContextWindowLimit - 3
            let recentMessages = 
                msgs 
                |> List.skip (msgs.Length - recentCount)
                |> List.skipWhile (function :? ChatRequestToolMessage -> true | _ -> false)

            let summaryMsg = ChatRequestSystemMessage(
                sprintf "[System: Previous conversation history was trimmed to fit context window. %d earlier messages summarized.]" (msgs.Length - recentMessages.Length - 1)
            )
            systemPrompt :: summaryMsg :: recentMessages

    member private self.ExecuteApiWithRetry(preparedMessages: ChatRequestMessage list, retryCount: int) : Async<Result<ChatCompletions, string>> =
        async {
            let reqOptions = ChatCompletionsOptions(config.Model, preparedMessages)
            reqOptions.Temperature <- Nullable(0.7f)

            let schemas = registry.GetToolSchemas()
            for schema in schemas do
                reqOptions.Tools.Add(ChatCompletionsFunctionToolDefinition(schema))

            try
                let! resp = client.GetChatCompletionsAsync(reqOptions) |> Async.AwaitTask
                return Ok resp.Value
            with ex ->
                printfn "  [API Error Retry %d/%d] %s" (retryCount + 1) config.MaxRetries ex.Message
                if retryCount >= config.MaxRetries - 1 then
                    return Error ex.Message
                else
                    let delayMs = (int (Math.Pow(2.0, float retryCount))) * 1000
                    do! Async.Sleep delayMs
                    return! self.ExecuteApiWithRetry(preparedMessages, retryCount + 1)
        }

    member self.RunAsync(userInput: string) : Async<TurnResult> =
        async {
            // --- Phase 1: Turn Prologue ---
            printfn "\nUser: %s" userInput
            messages <- messages @ [ ChatRequestUserMessage(userInput) ]

            let mutable apiCalls = 0
            interruptRequested <- false
            let mutable emptyContentRetries = 0
            let mutable turnResult : TurnResult option = None

            // --- Phase 2: Main Conversation Loop ---
            while apiCalls < config.MaxIterations && turnResult.IsNone do
                if interruptRequested then
                    printfn "  [Turn Exit] Turn interrupted by user."
                    turnResult <- Some {
                        FinalResponse = ""
                        Messages = messages
                        ApiCalls = apiCalls
                        Completed = false
                        Failed = false
                        Interrupted = true
                        ExitReason = Interrupted
                        Error = None
                    }
                else
                    apiCalls <- apiCalls + 1

                    // 2.2 & 2.3 Message Preparation and Context Compression via F# Pipeline
                    let preparedMessages = 
                        messages 
                        |> self.PrepareApiMessages 
                        |> self.CompressContextIfNeeded

                    // 2.4 Inner API Retry Loop
                    let! apiResult = self.ExecuteApiWithRetry(preparedMessages, 0)

                    match apiResult with
                    | Error err ->
                        turnResult <- Some {
                            FinalResponse = ""
                            Messages = messages
                            ApiCalls = apiCalls
                            Completed = false
                            Failed = true
                            Interrupted = false
                            ExitReason = ApiError err
                            Error = Some err
                        }
                    | Ok completions when completions.Choices.Count = 0 ->
                        turnResult <- Some {
                            FinalResponse = ""
                            Messages = messages
                            ApiCalls = apiCalls
                            Completed = false
                            Failed = true
                            Interrupted = false
                            ExitReason = NoResponse "No choices returned"
                            Error = Some "No choices returned from LLM"
                        }
                    | Ok completions ->
                        let choice = completions.Choices.[0]
                        let message = choice.Message

                        // 2.6 Tool Call Execution Path
                        if not (isNull message.ToolCalls) && message.ToolCalls.Count > 0 then
                            let assistantMsg = ChatRequestAssistantMessage(message.Content)
                            let registeredNames = registry.GetRegisteredNames() |> Set.ofList

                            let newToolMessages = 
                                message.ToolCalls
                                |> Seq.choose (function
                                    | :? ChatCompletionsFunctionToolCall as fnCall -> Some fnCall
                                    | _ -> None)
                                |> Seq.map (fun fnCall ->
                                    assistantMsg.ToolCalls.Add(fnCall)
                                    let name = fnCall.Name
                                    let callId = fnCall.Id
                                    let argsStr = fnCall.Arguments

                                    if not (registeredNames.Contains(name)) then
                                        let avail = registeredNames |> String.concat ", "
                                        let errStr = sprintf "Error: Tool '%s' is not registered. Available tools: %s" name avail
                                        printfn "  [Tool Validation Error] %s" errStr
                                        ChatRequestToolMessage(errStr, callId)
                                    else
                                        try
                                            use doc = JsonDocument.Parse(if String.IsNullOrEmpty argsStr then "{}" else argsStr)
                                            printfn "  [Tool Execution] %s(%s)" name argsStr
                                            let execResult = registry.ExecuteToolAsync(name, argsStr) |> Async.RunSynchronously
                                            printfn "  [Tool Result] %s" execResult
                                            ChatRequestToolMessage(execResult, callId)
                                        with jsonEx ->
                                            let errStr = sprintf "Error: Invalid JSON arguments for tool '%s': %s" name jsonEx.Message
                                            printfn "  [JSON Parse Error] %s" errStr
                                            ChatRequestToolMessage(errStr, callId)
                                )
                                |> Seq.toList

                            // Append assistant message and tool response messages to canonical history
                            messages <- messages @ (assistantMsg :> ChatRequestMessage :: (newToolMessages |> List.map (fun t -> t :> ChatRequestMessage)))
                        else
                            // 2.7 Final Text Response Path
                            let finalText = if isNull message.Content then "" else message.Content.Trim()

                            if String.IsNullOrEmpty finalText then
                                if emptyContentRetries < 2 then
                                    emptyContentRetries <- emptyContentRetries + 1
                                    printfn "  [Empty Response Recovery] Retrying with prompt nudge..."
                                    messages <- messages @ [ ChatRequestUserMessage("Please provide a complete text response summarizing your answer.") ]
                                else
                                    let text = "(empty response)"
                                    printfn "Assistant: %s" text
                                    messages <- messages @ [ ChatRequestAssistantMessage(text) ]
                                    turnResult <- Some {
                                        FinalResponse = text
                                        Messages = messages
                                        ApiCalls = apiCalls
                                        Completed = true
                                        Failed = false
                                        Interrupted = false
                                        ExitReason = TextResponse text
                                        Error = None
                                    }
                            else
                                printfn "Assistant: %s" finalText
                                messages <- messages @ [ ChatRequestAssistantMessage(finalText) ]

                                // --- Phase 4: Turn Finalization ---
                                turnResult <- Some {
                                    FinalResponse = finalText
                                    Messages = messages
                                    ApiCalls = apiCalls
                                    Completed = true
                                    Failed = false
                                    Interrupted = false
                                    ExitReason = TextResponse finalText
                                    Error = None
                                }

            match turnResult with
            | Some res -> return res
            | None ->
                printfn "  [Turn Exit] Reached max iterations (%d)." config.MaxIterations
                return {
                    FinalResponse = "Reached maximum iteration limit."
                    Messages = messages
                    ApiCalls = apiCalls
                    Completed = false
                    Failed = true
                    Interrupted = false
                    ExitReason = BudgetExhausted
                    Error = Some "Budget exhausted"
                }
        }
