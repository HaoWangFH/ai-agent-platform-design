namespace Skight.AgentPlatform.FSharp

open System
open System.Collections.Generic
open System.Text.Json
open Azure.AI.OpenAI

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
                assistant.ToolCalls.Add(ChatCompletionsFunctionToolCall(ToolCallId.value toolCall.Id, ToolName.value toolCall.Name, toolCall.ArgumentsJson))
            assistant :> ChatRequestMessage
        | ToolMessage (toolCallId, content) -> ChatRequestToolMessage(content, ToolCallId.value toolCallId) :> ChatRequestMessage

    let (|FunctionToolCall|_|) (tc: ChatCompletionsToolCall) =
        match tc with
        | :? ChatCompletionsFunctionToolCall as fnCall -> Some fnCall
        | _ -> None

    let toDomainResponse (responseMessage: ChatResponseMessage) : LlmTurnResponse =
        let content = if isNull responseMessage.Content then "" else responseMessage.Content
        let toolCalls =
            if isNull responseMessage.ToolCalls then
                []
            else
                responseMessage.ToolCalls
                |> Seq.choose (function
                    | FunctionToolCall fnCall ->
                        match ToolCallId.create fnCall.Id, ToolName.create fnCall.Name with
                        | Ok id, Ok name -> 
                            Some {
                                Id = id
                                Name = name
                                ArgumentsJson = fnCall.Arguments
                            }
                        | _ -> None
                    | _ -> None)
                |> Seq.toList

        {
            Content = content
            ToolCalls = toolCalls
        }

    let toFunctionDefinition (schema: ToolSchema) : FunctionDefinition =
        FunctionDefinition(
            Name = ToolName.value schema.Name,
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
                        return Error NoChoicesReturned
                    else
                        return Ok (toDomainResponse completions.Choices.[0].Message)
                with ex ->
                    return Error (ApiCallFailed ex.Message)
            }

    let systemPrompt =
        "You are a helpful AI assistant. You have access to various tools. " +
        "When asked to perform a task, use the tools to gather information and take actions before answering."

    let mutable sessionState : AgentSessionState =
        AgentSession.initialize systemPrompt

    member _.DefaultLlmCaller : LlmCaller = defaultLlmCaller

    [<Obsolete("Use pure AgentSession.requestInterrupt instead")>]
    member _.RequestInterrupt() =
        sessionState <- AgentSession.requestInterrupt sessionState

    /// Executes a turn using the composable functional loop pipeline
    [<Obsolete("Use AgentRunner.runTurnAsync for pure functional state management")>]
    member _.RunAsync(userInput: string, ?customLlmCaller: LlmCaller, ?customExecutor: ToolExecutor) : Async<TurnResult> =
        async {
            let activeLlmCaller = defaultArg customLlmCaller defaultLlmCaller
            let activeExecutor = defaultArg customExecutor registry.AsExecutor
            let registeredSchemas = registry.GetToolSchemas()
            let registeredNamesSet = registry.GetRegisteredNames() |> Set.ofList

            let! result, newState = AgentRunner.runTurnAsync activeLlmCaller activeExecutor config userInput sessionState registeredSchemas registeredNamesSet
            sessionState <- newState
            return result
        }
