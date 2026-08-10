namespace Skight.AgentPlatform.FSharp

module AgentSession =

    let initialize (systemPrompt: string) : AgentSessionState =
        {
            Messages = [ SystemMessage systemPrompt ]
            PendingCommand = RunTurn
        }

    let beginTurn (config: AgentConfig) (userInput: string) (session: AgentSessionState) : TurnState * AgentSessionState =
        let updatedMessages = session.Messages @ [ UserMessage userInput ]
        let turnState = {
            Messages = updatedMessages
            ApiCalls = 0
            EmptyContentRetries = 0
            Command = session.PendingCommand
            Config = config
            HasFileMutations = false
            HasExecutedVerification = false
            PreVerifyNudges = 0
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
