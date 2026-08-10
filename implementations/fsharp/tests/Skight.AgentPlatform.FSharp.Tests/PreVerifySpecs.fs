namespace Skight.AgentPlatform.FSharp.Tests

open System
open Expecto
open Skight.AgentPlatform.FSharp

module PreVerifySpecs =

    [<Tests>]
    let tests =
        testList "Pre-Verify Stop Gate Specs" [

            testAsync "Feature: Intercept completed turn when files modified without verification" {
                let mutable callCount = 0
                let mockLlm : LlmCaller =
                    fun _ msgs -> async {
                        callCount <- callCount + 1
                        if callCount = 1 then
                            let writeTool = ToolName.create "write_to_file" |> Result.toOption |> Option.get
                            let callId = ToolCallId.create "call_1" |> Result.toOption |> Option.get
                            return Ok {
                                Content = "I updated the code."
                                ToolCalls = [ { Id = callId; Name = writeTool; ArgumentsJson = "{}" } ]
                            }
                        else
                            return Ok {
                                Content = "Done with my changes."
                                ToolCalls = []
                            }
                    }

                let mockExecutor : ToolExecutor =
                    fun _ _ -> async { return "file written" }

                let writeTool = ToolName.create "write_to_file" |> Result.toOption |> Option.get
                let schema = { Name = writeTool; Description = "write file"; ParametersJson = "{}" }
                let config = { Model = "gpt-4o"; MaxIterations = 10; MaxRetries = 1; ContextWindowLimit = 20 }
                let session = AgentSession.initialize "system"

                let! (result, _) = AgentRunner.runTurnAsync mockLlm mockExecutor config "Fix bug" session [ schema ] (Set.singleton writeTool)

                Expect.equal callCount 4 "Should run 4 LLM calls (1 tool call + 2 verify nudges + 1 final completion)"
                Expect.equal result.ApiCalls 4 "ApiCalls should be 4"
                match result.Outcome with
                | Completed text -> Expect.stringContains text "Done with my changes" "Final text returned"
                | _ -> failwith "Expected completed turn"
            }
        ]
