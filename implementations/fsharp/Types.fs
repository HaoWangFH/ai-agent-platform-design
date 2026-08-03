namespace AgentPlatform.FSharp

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
