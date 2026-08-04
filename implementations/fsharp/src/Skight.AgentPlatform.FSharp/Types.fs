namespace Skight.AgentPlatform.FSharp

type ExitReason =
    | TextResponse of string
    | BudgetExhausted
    | Interrupted
    | ApiError of string
    | NoResponse of string

type TurnOutcome =
    | Completed of FinalResponse: string
    | Interrupted of Reason: ExitReason
    | Failed of Reason: ExitReason * ErrorMessage: string option

type ToolCall = {
    Id: string
    Name: string
    ArgumentsJson: string
}

type AgentMessage =
    | SystemMessage of Content: string
    | UserMessage of Content: string
    | AssistantMessage of Content: string * ToolCalls: ToolCall list
    | ToolMessage of ToolCallId: string * Content: string

type LlmTurnResponse = {
    Content: string
    ToolCalls: ToolCall list
}

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
    Messages: AgentMessage list
    PendingCommand: TurnCommand
}

type ToolDefinition = {
    Name: string
    Description: string
    ParametersJson: string
    Handler: string -> Async<string>
}

type ToolSchema = {
    Name: string
    Description: string
    ParametersJson: string
}

/// Immutable turn state passed through pure function pipelines
type TurnState = {
    Messages: AgentMessage list
    ApiCalls: int
    EmptyContentRetries: int
    Command: TurnCommand
    Config: AgentConfig
}

/// Control flow result for composable pipeline steps
type StepResult<'State, 'Result> =
    | Continue of 'State
    | Exit of 'Result

/// Composable function type signatures for dependency injection and partial application
type LlmCaller = ToolSchema list -> AgentMessage list -> Async<Result<LlmTurnResponse, string>>
type ToolExecutor = string -> string -> Async<string>
