namespace Skight.AgentPlatform.FSharp

open System
open System.Collections.Generic
open System.Text.Json
open System.Threading
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

    // Standard default Azure.AI.OpenAI LLM caller implementation
    let defaultLlmCaller : LlmCaller =
        fun schemas msgs ->
            async {
                let requestMessages = msgs |> List.map SdkAdapter.toChatRequestMessage
                let reqOptions = ChatCompletionsOptions(config.Model, requestMessages)
                reqOptions.Temperature <- Nullable(0.7f)
                for schema in schemas do
                    reqOptions.Tools.Add(ChatCompletionsFunctionToolDefinition(SdkAdapter.toFunctionDefinition schema))
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

    let defaultStreamingCallerFactory (cancellationToken: CancellationToken) (onChunk: StreamChunk -> unit) : StreamingLlmCaller =
        fun schemas msgs ->
            async {
                try
                    let stream = SdkAdapter.streamLlmResponseWithCallback client config schemas msgs cancellationToken onChunk
                    return Ok stream
                with ex ->
                    return Error (ApiCallFailed ex.Message)
            }

    let systemPrompt =
        "You are a helpful AI assistant. You have access to tools including file system operations and terminal command execution. " +
        "When asked to perform a task, inspect tool descriptions and use appropriate tools to gather information and take actions before answering."

    member _.SystemPrompt : string = systemPrompt

    member _.DefaultLlmCaller : LlmCaller = defaultLlmCaller

    member _.CreateInitialSession() : AgentSessionState =
        AgentSession.initialize systemPrompt

    member _.RunPureAsync(
        userInput: string,
        sessionState: AgentSessionState,
        ?customLlmCaller: LlmCaller,
        ?customExecutor: ToolExecutor,
        ?registeredSchemas: ToolSchema list,
        ?registeredNamesSet: Set<ToolName>
    ) : Async<TurnResult * AgentSessionState> =
        async {
            let activeLlmCaller = defaultArg customLlmCaller defaultLlmCaller
            let activeExecutor = defaultArg customExecutor registry.AsExecutor
            let schemas = defaultArg registeredSchemas (registry.GetToolSchemas())
            let namesSet = defaultArg registeredNamesSet (registry.GetRegisteredNames() |> Set.ofList)

            return! AgentRunner.runTurnAsync activeLlmCaller activeExecutor config userInput sessionState schemas namesSet
        }

    member _.RunPureStreamingAsync(
        userInput: string,
        sessionState: AgentSessionState,
        onChunk: StreamChunk -> unit,
        ?customStreamingLlmCallerFactory: (CancellationToken -> (StreamChunk -> unit) -> StreamingLlmCaller),
        ?customExecutor: ToolExecutor,
        ?registeredSchemas: ToolSchema list,
        ?registeredNamesSet: Set<ToolName>,
        ?cancellationToken: CancellationToken
    ) : Async<TurnResult * AgentSessionState> =
        async {
            let activeFactory = defaultArg customStreamingLlmCallerFactory defaultStreamingCallerFactory
            let activeExecutor = defaultArg customExecutor registry.AsExecutor
            let schemas = defaultArg registeredSchemas (registry.GetToolSchemas())
            let namesSet = defaultArg registeredNamesSet (registry.GetRegisteredNames() |> Set.ofList)
            let ct = defaultArg cancellationToken CancellationToken.None

            return! AgentRunner.runTurnStreamingAsync activeFactory activeExecutor config userInput sessionState schemas namesSet onChunk ct
        }
