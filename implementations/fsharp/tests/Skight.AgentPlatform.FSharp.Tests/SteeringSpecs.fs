namespace Skight.AgentPlatform.FSharp.Tests

open System
open Expecto
open Skight.AgentPlatform.FSharp

module SteeringSpecs =

    [<Tests>]
    let tests =
        testList "Pre-API Steering Drain Specs (/steer)" [

            testAsync "Feature: Drain mid-turn steering message into last tool message before LLM API call" {
                let mutable receivedSecondCallMsgs : AgentMessage list = []
                let mutable callCount = 0

                let mockLlm : LlmCaller =
                    fun _ msgs -> async {
                        callCount <- callCount + 1
                        if callCount = 1 then
                            let toolName = ToolName.create "read_file" |> Result.toOption |> Option.get
                            let callId = ToolCallId.create "call_1" |> Result.toOption |> Option.get
                            return Ok {
                                Content = "Reading config..."
                                ToolCalls = [ { Id = callId; Name = toolName; ArgumentsJson = "{}" } ]
                            }
                        else
                            receivedSecondCallMsgs <- msgs
                            return Ok {
                                Content = "Steered answer completed."
                                ToolCalls = []
                            }
                    }

                let readTool = ToolName.create "read_file" |> Result.toOption |> Option.get
                let schema = { Name = readTool; Description = "read file"; ParametersJson = "{}" }
                let config = { Model = "gpt-4o"; MaxIterations = 5; MaxRetries = 1; ContextWindowLimit = 20 }
                let session = AgentSession.initialize "system"

                let mockExecutor : ToolExecutor =
                    fun name args -> async {
                        AgentSession.enqueueSteering "Focus on HTTPS configuration instead of HTTP" session
                        return "file contents: port=8080"
                    }

                let! (result, _) = AgentRunner.runTurnAsync mockLlm mockExecutor config "Start server" session [ schema ] (Set.singleton readTool)

                Expect.equal result.ApiCalls 2 "Should complete in 2 API calls"
                match List.tryLast receivedSecondCallMsgs with
                | Some (ToolMessage(_, content)) ->
                    Expect.stringContains content "[USER STEERING INTERRUPT]: Focus on HTTPS configuration instead of HTTP" "Steering content merged into tool message"
                | _ -> failwith "Expected last message to be ToolMessage with steering content"
            }
        ]
