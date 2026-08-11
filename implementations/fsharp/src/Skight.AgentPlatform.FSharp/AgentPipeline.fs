namespace Skight.AgentPlatform.FSharp

open System
open System.Collections.Generic
open System.Text
open System.Text.Json
open Azure.AI.OpenAI

type BearerTokenPolicy(token: string) =
    inherit Azure.Core.Pipeline.HttpPipelineSynchronousPolicy()
    override _.OnSendingRequest(message: Azure.Core.HttpMessage) =
        message.Request.Headers.SetValue("Authorization", sprintf "Bearer %s" token)

module AgentPipeline =

    type PartialToolCall = {
        Id: ToolCallId option
        Name: ToolName option
        ArgsBuilder: StringBuilder
    }

    let private emptyPartialToolCall = {
        Id = None
        Name = None
        ArgsBuilder = StringBuilder()
    }

    let private upsertPartialToolCall (index: int) (idOpt: ToolCallId option) (nameOpt: ToolName option) (argsFragment: string) (partials: Map<int, PartialToolCall>) =
        let current = partials |> Map.tryFind index |> Option.defaultValue emptyPartialToolCall
        let nextId = if idOpt.IsSome then idOpt else current.Id
        let nextName = if nameOpt.IsSome then nameOpt else current.Name
        if not (String.IsNullOrEmpty(argsFragment)) then
            current.ArgsBuilder.Append(argsFragment) |> ignore

        partials
        |> Map.add index {
            Id = nextId
            Name = nextName
            ArgsBuilder = current.ArgsBuilder
        }

    let private toToolCalls (partials: Map<int, PartialToolCall>) : ToolCall list =
        partials
        |> Seq.sortBy (fun kv -> kv.Key)
        |> Seq.choose (fun kv ->
            match kv.Value.Id, kv.Value.Name with
            | Some id, Some name ->
                Some {
                    Id = id
                    Name = name
                    ArgumentsJson = kv.Value.ArgsBuilder.ToString()
                }
            | _ -> None)
        |> Seq.toList

    let aggregateStreamResponse (stream: System.Collections.Generic.IAsyncEnumerable<StreamChunk>) : Async<Result<LlmTurnResponse, StreamAggregationError>> =
        async {
            let textBuilder = StringBuilder()
            let mutable hasCompleted = false
            let mutable finishReason = ""
            let mutable partialToolCalls = Map.empty<int, PartialToolCall>

            try
                let enumerator = stream.GetAsyncEnumerator()
                let mutable keepReading = true

                while keepReading do
                    let! hasNext = enumerator.MoveNextAsync().AsTask() |> Async.AwaitTask
                    if not hasNext then
                        keepReading <- false
                    else
                        match enumerator.Current with
                        | TextDelta content ->
                            if not (String.IsNullOrEmpty(content)) then
                                textBuilder.Append(content) |> ignore
                        | ToolCallDelta (index, idOpt, nameOpt, argsFragment) ->
                            partialToolCalls <- upsertPartialToolCall index idOpt nameOpt argsFragment partialToolCalls
                        | StreamCompleted reason ->
                            hasCompleted <- true
                            finishReason <- reason

                let text = textBuilder.ToString()
                let toolCalls = toToolCalls partialToolCalls

                if hasCompleted then
                    let normalized = if isNull finishReason then "" else finishReason.Trim().ToLowerInvariant()
                    if normalized = "length" then
                        return Error (PartialResponse text)
                    else
                        return Ok { Content = text; ToolCalls = toolCalls }
                else
                    if String.IsNullOrWhiteSpace(text) then
                        return Error (PartialResponse "")
                    else
                        return Error (PartialResponse text)
            with _ ->
                let partialText = textBuilder.ToString()
                return Error (PartialResponse partialText)
        }

    let streamToLlmResponse (streamingCaller: StreamingLlmCaller) (schemas: ToolSchema list) (msgs: AgentMessage list) : Async<Result<LlmTurnResponse, LlmError>> =
        async {
            let! streamResult = streamingCaller schemas msgs
            match streamResult with
            | Error err -> return Error err
            | Ok stream ->
                let! aggregated = aggregateStreamResponse stream
                match aggregated with
                | Ok response -> return Ok response
                | Error (PartialResponse partialText) when not (String.IsNullOrWhiteSpace(partialText)) ->
                    return Ok { Content = partialText; ToolCalls = [] }
                | Error _ ->
                    return Error NoChoicesReturned
        }

    /// Step 2.1a: Pre-API Check - Interrupt Guard
    let checkInterrupt (state: TurnState) : StepResult<TurnState, TurnResult> =
        match state.Command with
        | InterruptTurn ->
            printfn "  [Turn Exit] Turn interrupted by user."
            Exit {
                Outcome = TurnOutcome.Interrupted
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
                Outcome = TurnOutcome.Failed (FailureReason.BudgetExhausted "Budget exhausted")
                Messages = state.Messages
                ApiCalls = state.ApiCalls
            }
        else
            Continue { state with ApiCalls = state.ApiCalls + 1 }

    /// Step 2.2: Prepare Messages Payload (shallow copy pipeline step)
    let prepareApiMessages (msgs: AgentMessage list) : AgentMessage list =
        msgs |> List.map id

    /// Step 2.3: Context Window Protection & Compaction Engine (Pure Pipeline Transformation)
    let compressContextIfNeeded (limit: int) (msgs: AgentMessage list) : AgentMessage list =
        ContextCompressor.compress 0.80 limit msgs

    /// Pipeline composition for message payload preparation
    let preparePayload (limit: int) (msgs: AgentMessage list) : AgentMessage list =
        msgs
        |> prepareApiMessages
        |> compressContextIfNeeded limit

    let llmErrorMessage (err: LlmError) =
        match err with
        | NoChoicesReturned -> "No choices returned from LLM"
        | ApiCallFailed message -> message

    /// Step 2.4: Execute API Call with Exponential Backoff Retry (Pure Async Recursion)
    let rec callLlmWithRetry (llmCaller: LlmCaller) (schemas: ToolSchema list) (maxRetries: int) (retryCount: int) (msgs: AgentMessage list) : Async<Result<LlmTurnResponse, LlmError>> =
        async {
            let! result = llmCaller schemas msgs
            match result with
            | Ok response -> return Ok response
            | Error err ->
                printfn "  [API Error Retry %d/%d] %s" (retryCount + 1) maxRetries (llmErrorMessage err)
                if retryCount >= maxRetries - 1 then
                    return Error err
                else
                    let delayMs = (int (Math.Pow(2.0, float retryCount))) * 1000
                    do! Async.Sleep delayMs
                    return! callLlmWithRetry llmCaller schemas maxRetries (retryCount + 1) msgs
        }

    let private cleanJsonArgs (argsStr: string) =
        let mutable clean = if String.IsNullOrWhiteSpace argsStr then "{}" else argsStr.Trim()

        let extractBraces () =
            let braceIndex = clean.IndexOf('{')
            if braceIndex > 0 then
                let lastBrace = clean.LastIndexOf('}')
                if lastBrace > braceIndex then
                    clean <- clean.Substring(braceIndex, lastBrace - braceIndex + 1)

        extractBraces ()

        if clean.StartsWith("\"") && clean.EndsWith("\"") then
            try
                let unescaped = JsonSerializer.Deserialize<string>(clean)
                if not (String.IsNullOrWhiteSpace unescaped) then clean <- unescaped.Trim()
            with _ -> ()

        extractBraces ()

        if not (clean.StartsWith("{")) then
            let rawString = clean.Trim('"')
            let dict = dict [ "path", rawString; "command", rawString; "key", rawString; "task", rawString; "text", rawString; "url", rawString ]
            clean <- JsonSerializer.Serialize dict

        clean

    /// Step 2.6: Process Tool Calls (Self-Correction, JSON Validation & Execution)
    let processToolCalls (executor: ToolExecutor) (registeredNamesSet: Set<ToolName>) (content: string) (toolCalls: ToolCall list) (state: TurnState) : Async<TurnState> =
        async {
            let! newToolMessages =
                toolCalls
                |> Seq.map (fun toolCall ->
                    async {
                        let name = toolCall.Name
                        let callId = toolCall.Id
                        let argsStr = cleanJsonArgs toolCall.ArgumentsJson
                        
                        let nameStr = ToolName.value name

                        if not (registeredNamesSet.Contains(name)) then
                            let avail = registeredNamesSet |> Seq.map ToolName.value |> String.concat ", "
                            let errStr = sprintf "Error: Tool '%s' is not registered. Available tools: %s" nameStr avail
                            printfn "  [Tool Validation Error] %s" errStr
                            return ToolMessage(callId, errStr)
                        else
                            try
                                use doc = JsonDocument.Parse(argsStr)
                                printfn "  [Tool Execution] %s(%s)" nameStr argsStr
                                let swTool = System.Diagnostics.Stopwatch.StartNew()
                                let! execResult = executor name argsStr
                                swTool.Stop()
                                AgentTelemetry.trackToolExecution state.SessionId state.UserId state.TurnIndex nameStr swTool.ElapsedMilliseconds argsStr execResult (Some state.SessionId) (Some state.TurnSpanId)
                                printfn "  [Tool Result] %s" execResult
                                return ToolMessage(callId, execResult)
                            with jsonEx ->
                                let errStr = sprintf "Error: Invalid JSON arguments for tool '%s': %s" nameStr jsonEx.Message
                                printfn "  [JSON Parse Error] %s" errStr
                                return ToolMessage(callId, errStr)
                    })
                |> Async.Parallel

            let mutationNames = ["file_write"; "write_to_file"; "file_patch"; "replace_file_content"]
            let verificationNames = ["read_terminal"; "terminal_execute"; "run_tests"; "dotnet_test"]

            let executedNames = toolCalls |> List.map (fun tc -> ToolName.value tc.Name)
            let hasMutation = state.HasFileMutations || (executedNames |> List.exists (fun n -> List.contains n mutationNames))
            let hasVerification = 
                if executedNames |> List.exists (fun n -> List.contains n verificationNames) then true
                elif executedNames |> List.exists (fun n -> List.contains n mutationNames) then false
                else state.HasExecutedVerification

            let assistantMsg = AssistantMessage(content, toolCalls)
            let updatedHistory = state.Messages @ (assistantMsg :: (newToolMessages |> Array.toList))
            return { state with Messages = updatedHistory; HasFileMutations = hasMutation; HasExecutedVerification = hasVerification }
        }

    /// Step 2.7: Process Final Text Response with Empty Response Recovery & Pre-Verify Gate
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
            if state.HasFileMutations && not state.HasExecutedVerification && state.PreVerifyNudges < 2 then
                printfn "  [Pre-Verify Quality Gate] Intercepted completed turn. Files modified without verification. Prompting agent to verify..."
                let nudgeMsg = UserMessage "You modified files during this turn. Please verify your changes by executing unit tests or build commands before concluding."
                let updatedHistory = state.Messages @ [ AssistantMessage(finalText, []); nudgeMsg ]
                Continue { state with Messages = updatedHistory; PreVerifyNudges = state.PreVerifyNudges + 1 }
            else
                AgentTelemetry.trackTurnEnd state.SessionId state.UserId state.TurnIndex 0L finalText "completed" (Some state.SessionId) (Some state.TurnSpanId)
                let updatedHistory = state.Messages @ [ AssistantMessage(finalText, []) ]
                Exit {
                    Outcome = TurnOutcome.Completed finalText
                    Messages = updatedHistory
                    ApiCalls = state.ApiCalls
                }

    /// Step 2.1b: Pre-API Steering Drain (/steer)
    let drainSteering (queue: System.Collections.Concurrent.ConcurrentQueue<string>) (history: AgentMessage list) : AgentMessage list =
        if isNull queue then history else
        let items = System.Collections.Generic.List<string>()
        let mutable item = ""
        while queue.TryDequeue(&item) do
            if not (System.String.IsNullOrWhiteSpace item) then
                items.Add(item)

        if items.Count = 0 then
            history
        else
            let steeringContent = "\n\n[USER STEERING INTERRUPT]: " + String.concat "\n" items
            printfn "  [Pre-API Steering Drain] Injected %d mid-turn steering message(s)" items.Count

            match List.rev history with
            | ToolMessage(callId, content) :: rest ->
                let updatedToolMsg = ToolMessage(callId, content + steeringContent)
                List.rev (updatedToolMsg :: rest)
            | UserMessage content :: rest ->
                let updatedUserMsg = UserMessage(content + steeringContent)
                List.rev (updatedUserMsg :: rest)
            | _ ->
                history @ [ UserMessage ("[USER STEERING INTERRUPT]: " + String.concat "\n" items) ]

    /// Pure, Tail-Recursive 4-Phase Turn Loop Function
    let rec runTurnLoop (llmCaller: LlmCaller) (executor: ToolExecutor) (registeredSchemas: ToolSchema list) (registeredNamesSet: Set<ToolName>) (state: TurnState) : Async<TurnResult> =
        async {
            // Pipeline Step 2.1: Pre-API Checks (Interrupt Guard -> Budget Guard)
            match checkInterrupt state with
            | Exit result -> return result
            | Continue stateAfterInterruptCheck ->

            match checkBudget stateAfterInterruptCheck with
            | Exit result -> return result
            | Continue stateAfterBudgetCheck ->

            // Step 2.1b: Pre-API Steering Drain
            let stateDrained =
                if not (isNull stateAfterBudgetCheck.SteeringQueue) then
                    let updatedMessages = drainSteering stateAfterBudgetCheck.SteeringQueue stateAfterBudgetCheck.Messages
                    { stateAfterBudgetCheck with Messages = updatedMessages }
                else stateAfterBudgetCheck

            // Pipeline Step 2.2 & 2.3: Message Payload Preparation & Context Compression
            let preparedPayload = preparePayload stateDrained.Config.ContextWindowLimit stateDrained.Messages

            // Pipeline Step 2.4: Inner LLM API Call with Retry
            let swLlm = System.Diagnostics.Stopwatch.StartNew()
            let! apiResult = callLlmWithRetry llmCaller registeredSchemas stateDrained.Config.MaxRetries 0 preparedPayload
            swLlm.Stop()

            match apiResult with
            | Error NoChoicesReturned ->
                let message = llmErrorMessage NoChoicesReturned
                let res = { Outcome = TurnOutcome.Failed (FailureReason.NoResponse message); Messages = stateDrained.Messages; ApiCalls = stateDrained.ApiCalls }
                AgentTelemetry.trackTurnEnd stateDrained.SessionId stateDrained.UserId stateDrained.TurnIndex 0L message "no_response" (Some stateDrained.SessionId) (Some stateDrained.TurnSpanId)
                return res
            | Error (ApiCallFailed err) ->
                let res = { Outcome = TurnOutcome.Failed (FailureReason.ApiError err); Messages = stateDrained.Messages; ApiCalls = stateDrained.ApiCalls }
                AgentTelemetry.trackTurnEnd stateDrained.SessionId stateDrained.UserId stateDrained.TurnIndex 0L err "api_error" (Some stateDrained.SessionId) (Some stateDrained.TurnSpanId)
                return res
            | Ok response ->
                AgentTelemetry.trackLlmCall stateDrained.SessionId stateDrained.UserId stateDrained.TurnIndex "gpt-4o" swLlm.ElapsedMilliseconds response.Content response.ToolCalls (Some stateDrained.SessionId) (Some stateDrained.TurnSpanId)

                // Pipeline Step 2.6: Tool Call Execution Path
                if response.ToolCalls.Length > 0 then
                    let! nextState = processToolCalls executor registeredNamesSet response.Content response.ToolCalls stateDrained
                    return! runTurnLoop llmCaller executor registeredSchemas registeredNamesSet nextState

                // Pipeline Step 2.7: Final Text Response Path
                else
                    match processTextResponse response.Content stateDrained with
                    | Exit turnResult ->
                        let respText = match turnResult.Outcome with TurnOutcome.Completed txt -> txt | TurnOutcome.Failed (FailureReason.ApiError e) -> e | _ -> ""
                        AgentTelemetry.trackTurnEnd stateDrained.SessionId stateDrained.UserId stateDrained.TurnIndex 0L respText "completed" (Some stateDrained.SessionId) (Some stateDrained.TurnSpanId)
                        return turnResult
                    | Continue nextState -> return! runTurnLoop llmCaller executor registeredSchemas registeredNamesSet nextState
        }
