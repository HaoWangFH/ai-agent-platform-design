namespace Skight.AgentPlatform.FSharp

open System.Collections.Generic

type ToolCallId = private ToolCallId of string
module ToolCallId =
    let create (id: string) =
        if System.String.IsNullOrWhiteSpace(id) then Error "ToolCallId cannot be empty"
        else Ok (ToolCallId id)
    let value (ToolCallId id) = id

type ToolName = private ToolName of string
module ToolName =
    let create (name: string) =
        if System.String.IsNullOrWhiteSpace(name) then Error "ToolName cannot be empty"
        else Ok (ToolName name)
    let value (ToolName name) = name

type FailureReason =
    | BudgetExhausted of string
    | ApiError of string
    | NoResponse of string

type TurnOutcome =
    | Completed of FinalResponse: string
    | Interrupted
    | Failed of Reason: FailureReason

type ToolCall = {
    Id: ToolCallId
    Name: ToolName
    ArgumentsJson: string
}

type AgentMessage =
    | SystemMessage of Content: string
    | UserMessage of Content: string
    | AssistantMessage of Content: string * ToolCalls: ToolCall list
    | ToolMessage of ToolCallId: ToolCallId * Content: string

type LlmTurnResponse = {
    Content: string
    ToolCalls: ToolCall list
}

type StreamChunk =
    | TextDelta of Content: string
    | ToolCallDelta of Index: int * Id: ToolCallId option * Name: ToolName option * ArgsFragment: string
    | StreamCompleted of FinishReason: string

type StreamAggregationError =
    | PartialResponse of PartialText: string

type LlmError =
    | NoChoicesReturned
    | ApiCallFailed of Message: string

type TurnResult = {
    Outcome: TurnOutcome
    Messages: AgentMessage list
    ApiCalls: int
}

type AgentConfig = {
    MaxIterations: int
    MaxRetries: int
    ContextWindowLimit: int
    Model: string
}

type TurnCommand =
    | RunTurn
    | InterruptTurn

type AgentSessionState = {
    SessionId: string
    UserId: string
    TurnIndex: int
    Messages: AgentMessage list
    PendingCommand: TurnCommand
    SteeringQueue: System.Collections.Concurrent.ConcurrentQueue<string>
}

type ToolDefinition = {
    Name: ToolName
    Description: string
    ParametersJson: string
    Handler: string -> Async<string>
}

type ToolSchema = {
    Name: ToolName
    Description: string
    ParametersJson: string
}

/// Immutable turn state passed through pure function pipelines
type TurnState = {
    SessionId: string
    UserId: string
    TurnIndex: int
    Messages: AgentMessage list
    ApiCalls: int
    EmptyContentRetries: int
    Command: TurnCommand
    Config: AgentConfig
    HasFileMutations: bool
    HasExecutedVerification: bool
    PreVerifyNudges: int
    SteeringQueue: System.Collections.Concurrent.ConcurrentQueue<string>
}

/// Control flow result for composable pipeline steps
type StepResult<'State, 'Result> =
    | Continue of 'State
    | Exit of 'Result

/// Composable function type signatures for dependency injection and partial application
type LlmCaller = ToolSchema list -> AgentMessage list -> Async<Result<LlmTurnResponse, LlmError>>
type StreamingLlmCaller = ToolSchema list -> AgentMessage list -> Async<Result<IAsyncEnumerable<StreamChunk>, LlmError>>
type ToolExecutor = ToolName -> string -> Async<string>
