namespace Skight.AgentPlatform.FSharp.Tests

open System
open Expecto
open Skight.AgentPlatform.FSharp

module SecurityGuardSpecs =

    [<Tests>]
    let tests =
        testList "Security & Approval Guard Module Specs" [

            test "Feature: Intercepts dangerous shell operations" {
                let dangerousCommand = "rm -rf /"
                let safeCommand = "echo Hello FSharp"

                let isDangerous = ApprovalGuard.isHighRiskCommand dangerousCommand
                let isSafe = ApprovalGuard.isHighRiskCommand safeCommand

                Expect.isTrue isDangerous "rm -rf must be flagged as high risk"
                Expect.isFalse isSafe "echo Hello FSharp must not be flagged as high risk"

                let mockRejectPrompt : ApprovalGuard.ApprovalPrompt = fun req -> async { return ApprovalGuard.Denied "User rejected action." }
                let result = ApprovalGuard.requireCommandApproval mockRejectPrompt dangerousCommand |> Async.RunSynchronously

                match result with
                | Error msg -> Expect.equal msg "User rejected action." "Security guard should deny dangerous command"
                | Ok () -> failwith "Dangerous command should not be approved"
            }
        ]
