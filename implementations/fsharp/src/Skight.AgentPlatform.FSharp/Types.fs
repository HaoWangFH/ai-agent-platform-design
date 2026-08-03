namespace Skight.AgentPlatform.FSharp

open System
open Azure.AI.OpenAI

type ExitReason =
    | TextResponse of string
    | BudgetExhausted
    | Interrupted
    | ApiError of string
    | NoResponse of string

type TurnResult = {
    FinalResponse: string
    Messages: ChatRequestMessage list
    ApiCalls: int
    Completed: bool
    Failed: bool
    Interrupted: bool
    ExitReason: ExitReason
    Error: string option
}

type AgentConfig = {
    MaxIterations: int
    MaxRetries: int
    ContextWindowLimit: int
    Model: string
}

type ToolDefinition = {
    Name: string
    Description: string
    ParametersJson: string
    Handler: string -> Async<string>
}

/// Immutable turn state passed through pure function pipelines
type TurnState = {
    Messages: ChatRequestMessage list
    ApiCalls: int
    EmptyContentRetries: int
    InterruptRequested: bool
    Config: AgentConfig
}

/// Control flow result for composable pipeline steps
type StepResult<'State, 'Result> =
    | Continue of 'State
    | Exit of 'Result

/// Composable function type signatures for dependency injection and partial application
type LlmCaller = FunctionDefinition list -> ChatRequestMessage list -> Async<Result<ChatCompletions, string>>
type ToolExecutor = string -> string -> Async<string>
