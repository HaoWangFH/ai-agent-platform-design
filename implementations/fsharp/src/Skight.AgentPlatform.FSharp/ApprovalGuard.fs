namespace Skight.AgentPlatform.FSharp

open System
open System.Text.RegularExpressions

module ApprovalGuard =
    type ApprovalDecision =
        | Approved
        | Denied of string

    type ApprovalRequest = {
        Action: string
        Reason: string
        Payload: string
    }

    type ApprovalPrompt = ApprovalRequest -> Async<ApprovalDecision>

    let private riskyCommandPattern =
        Regex(@"\b(rm\s+-rf|del\s+/f|format\b|shutdown\b|reboot\b|mkfs\b|diskpart\b|sudo\b)\b", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)

    let private riskyEditPattern =
        Regex(@"(?i)(password|secret|token|private[_-]?key|connectionstring)", RegexOptions.Compiled)

    let isHighRiskCommand (command: string) =
        not (String.IsNullOrWhiteSpace(command)) && riskyCommandPattern.IsMatch(command)

    let isHighRiskFileEdit (path: string) (contentPreview: string) =
        let sensitiveFile =
            not (String.IsNullOrWhiteSpace(path)) &&
            (path.EndsWith(".env", StringComparison.OrdinalIgnoreCase)
             || path.EndsWith("secrets.json", StringComparison.OrdinalIgnoreCase)
             || path.Contains("credential", StringComparison.OrdinalIgnoreCase))

        sensitiveFile || (not (String.IsNullOrWhiteSpace(contentPreview)) && riskyEditPattern.IsMatch(contentPreview))

    let createConsolePrompt () : ApprovalPrompt =
        fun request ->
            async {
                printfn "\nApproval required for action: %s" request.Action
                printfn "Reason: %s" request.Reason
                printfn "Payload: %s" request.Payload
                printf "Allow? (y/N): "
                let input = Console.ReadLine()
                if not (isNull input) && input.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) then
                    return Approved
                else
                    return Denied "User rejected action."
            }

    let enforceApproval (prompt: ApprovalPrompt) (request: ApprovalRequest) : Async<Result<unit, string>> =
        async {
            let! decision = prompt request
            match decision with
            | Approved -> return Ok ()
            | Denied reason -> return Error reason
        }

    let requireCommandApproval (prompt: ApprovalPrompt) (command: string) : Async<Result<unit, string>> =
        async {
            if isHighRiskCommand command then
                let request = {
                    Action = "execute_command"
                    Reason = "Command classified as high-risk."
                    Payload = command
                }
                return! enforceApproval prompt request
            else
                return Ok ()
        }

    let requireFileEditApproval (prompt: ApprovalPrompt) (path: string) (contentPreview: string) : Async<Result<unit, string>> =
        async {
            if isHighRiskFileEdit path contentPreview then
                let request = {
                    Action = "edit_file"
                    Reason = "File edit classified as sensitive/high-risk."
                    Payload = sprintf "%s | %s" path contentPreview
                }
                return! enforceApproval prompt request
            else
                return Ok ()
        }
