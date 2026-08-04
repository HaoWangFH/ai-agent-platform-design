namespace Skight.AgentPlatform.FSharp

open System
open System.Threading
open Azure.AI.OpenAI
open FSharp.Control

module SdkAdapter =

    let private toChatRequestMessage (msg: AgentMessage) : ChatRequestMessage =
        match msg with
        | SystemMessage content -> ChatRequestSystemMessage(content) :> ChatRequestMessage
        | UserMessage content -> ChatRequestUserMessage(content) :> ChatRequestMessage
        | AssistantMessage (content, toolCalls) ->
            let assistant = ChatRequestAssistantMessage(content)
            for toolCall in toolCalls do
                assistant.ToolCalls.Add(ChatCompletionsFunctionToolCall(ToolCallId.value toolCall.Id, ToolName.value toolCall.Name, toolCall.ArgumentsJson))
            assistant :> ChatRequestMessage
        | ToolMessage (toolCallId, content) ->
            ChatRequestToolMessage(content, ToolCallId.value toolCallId) :> ChatRequestMessage

    let private toFunctionDefinition (schema: ToolSchema) : FunctionDefinition =
        FunctionDefinition(
            Name = ToolName.value schema.Name,
            Description = schema.Description,
            Parameters = BinaryData.FromString(schema.ParametersJson)
        )

    let private optionOfResult r =
        match r with
        | Ok x -> Some x
        | Error _ -> None

    let private toToolCallDelta (update: StreamingChatCompletionsUpdate) =
        if isNull update.ToolCallUpdate then
            None
        else
            let tc = update.ToolCallUpdate
            let idOpt =
                if String.IsNullOrWhiteSpace(tc.Id) then None
                else ToolCallId.create tc.Id |> optionOfResult

            match tc with
            | :? StreamingFunctionToolCallUpdate as fnUpdate ->
                let nameOpt =
                    if String.IsNullOrWhiteSpace(fnUpdate.Name) then None
                    else ToolName.create fnUpdate.Name |> optionOfResult

                let argsFragment = if isNull fnUpdate.ArgumentsUpdate then "" else fnUpdate.ArgumentsUpdate
                Some (ToolCallDelta(tc.ToolCallIndex, idOpt, nameOpt, argsFragment))
            | _ ->
                Some (ToolCallDelta(tc.ToolCallIndex, idOpt, None, ""))

    /// Maps SDK streaming updates into a pure StreamChunk sequence with heartbeat/cancellation guard.
    let streamLlmResponse
        (client: OpenAIClient)
        (config: AgentConfig)
        (schemas: ToolSchema list)
        (messages: AgentMessage list)
        (cancellationToken: CancellationToken)
        : System.Collections.Generic.IAsyncEnumerable<StreamChunk> =
        taskSeq {
            let requestMessages = messages |> List.map toChatRequestMessage
            let reqOptions = ChatCompletionsOptions(config.Model, requestMessages)

            for schema in schemas do
                reqOptions.Tools.Add(ChatCompletionsFunctionToolDefinition(toFunctionDefinition schema))

            use cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            cts.CancelAfter(90000)

            let! response = client.GetChatCompletionsStreamingAsync(reqOptions, cts.Token)

            for update in response do
                cts.CancelAfter(90000)

                if cancellationToken.IsCancellationRequested then
                    yield StreamCompleted "interrupted_by_user"
                else
                    if not (String.IsNullOrEmpty(update.ContentUpdate)) then
                        yield TextDelta update.ContentUpdate

                    match toToolCallDelta update with
                    | Some toolDelta -> yield toolDelta
                    | None -> ()

                    if update.FinishReason.HasValue then
                        yield StreamCompleted (update.FinishReason.Value.ToString())

            if cancellationToken.IsCancellationRequested then
                yield StreamCompleted "interrupted_by_user"
        }

