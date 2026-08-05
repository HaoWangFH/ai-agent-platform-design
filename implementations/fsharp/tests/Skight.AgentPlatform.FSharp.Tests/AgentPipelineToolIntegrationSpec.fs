namespace Skight.AgentPlatform.FSharp.Tests

open System
open System.IO
open Expecto
open Skight.AgentPlatform.FSharp

module AgentPipelineToolIntegrationSpec =

    let private testWorkspace = Path.Combine(Path.GetTempPath(), "fsharp_pipeline_integration_sandbox", Guid.NewGuid().ToString("N"))

    let private ensureWorkspace () =
        Directory.CreateDirectory(testWorkspace) |> ignore
        testWorkspace

    let private isWindows = Environment.OSVersion.Platform = PlatformID.Win32NT

    [<Tests>]
    let agentPipelineToolIntegrationTests =
        testList "Level 2 & 3 Agent Pipeline & Tool Integration Specification Tests" [

            testAsync "Agent turn loop executes write_file -> edit_file -> execute_command -> read_file sequence" {
                let workspace = ensureWorkspace ()
                let callCounter = ref 0

                let targetFile = Path.Combine(workspace, "pipeline_test.txt")
                let safeTargetRelative = "pipeline_test.txt"

                let mockLlmCaller : LlmCaller =
                    fun _ _ -> async {
                        incr callCounter
                        match !callCounter with
                        | 1 ->
                            let args = sprintf """{"path":"%s","content":"initial line 1\ninitial line 2"}""" safeTargetRelative
                            let toolCall = {
                                Id = ToolCallId.create "call_1" |> Result.defaultWith failwith
                                Name = ToolName.create "write_file" |> Result.defaultWith failwith
                                ArgumentsJson = args
                            }
                            return Ok { Content = ""; ToolCalls = [ toolCall ] }
                        | 2 ->
                            let patchText = "<<<<<<< SEARCH\\ninitial line 2\\n=======\\npatched line 2\\n>>>>>>> REPLACE"
                            let args = sprintf """{"path":"%s","patch":"%s"}""" safeTargetRelative patchText
                            let toolCall = {
                                Id = ToolCallId.create "call_2" |> Result.defaultWith failwith
                                Name = ToolName.create "edit_file" |> Result.defaultWith failwith
                                ArgumentsJson = args
                            }
                            return Ok { Content = ""; ToolCalls = [ toolCall ] }
                        | 3 ->
                            let echoCmd = if isWindows then "echo PipelineCommandOk" else "echo PipelineCommandOk"
                            let args = sprintf """{"command":"%s"}""" echoCmd
                            let toolCall = {
                                Id = ToolCallId.create "call_3" |> Result.defaultWith failwith
                                Name = ToolName.create "execute_command" |> Result.defaultWith failwith
                                ArgumentsJson = args
                            }
                            return Ok { Content = ""; ToolCalls = [ toolCall ] }
                        | 4 ->
                            let args = sprintf """{"path":"%s"}""" safeTargetRelative
                            let toolCall = {
                                Id = ToolCallId.create "call_4" |> Result.defaultWith failwith
                                Name = ToolName.create "read_file" |> Result.defaultWith failwith
                                ArgumentsJson = args
                            }
                            return Ok { Content = ""; ToolCalls = [ toolCall ] }
                        | 5 ->
                            return Ok { Content = "All Task 3 tool actions completed successfully."; ToolCalls = [] }
                        | _ -> return Error (ApiCallFailed "Unexpected LLM call count")
                    }

                let registry = ToolRegistry()
                registry.Register("write_file", "Writes file", FileTools.writeFileTool workspace, "{}")
                registry.Register("edit_file", "Edits file", FileTools.editFileTool workspace, "{}")
                registry.Register("read_file", "Reads file", FileTools.readFileTool workspace, "{}")
                registry.Register("execute_command", "Executes command", (fun args ->
                    async {
                        use doc = System.Text.Json.JsonDocument.Parse(args)
                        let cmd = doc.RootElement.GetProperty("command").GetString()
                        return! TerminalTool.executeCommandAsync 5000 1024 cmd
                    }), "{}")

                let initialState : TurnState = {
                    Messages = [ SystemMessage "sys"; UserMessage "Execute Task 3 integration flow" ]
                    ApiCalls = 0
                    EmptyContentRetries = 0
                    Command = RunTurn
                    Config = { MaxIterations = 10; MaxRetries = 2; ContextWindowLimit = 10; Model = "test-model" }
                }

                let registeredNamesSet = registry.GetRegisteredNames() |> Set.ofList

                let! result = AgentPipeline.runTurnLoop mockLlmCaller registry.AsExecutor [] registeredNamesSet initialState

                match result.Outcome with
                | TurnOutcome.Completed finalText ->
                    Expect.equal finalText "All Task 3 tool actions completed successfully." "Final assistant text"
                    Expect.equal result.ApiCalls 5 "Expected 5 LLM API turn iterations"
                    Expect.isTrue (File.Exists(targetFile)) "File must exist on disk"
                    let fileContent = File.ReadAllText(targetFile)
                    Expect.stringContains fileContent "patched line 2" "Patched line must be present in file on disk"
                | outcome -> failtestf "Expected completed outcome, got %A" outcome
            }

            testAsync "AgentRunner.runTurnAsync handles multi-turn sessions with tool calls and state persistence across turns" {
                let workspace = ensureWorkspace ()
                let registry = ToolRegistry()
                registry.Register("write_file", "Writes file", FileTools.writeFileTool workspace, "{}")
                registry.Register("edit_file", "Edits file", FileTools.editFileTool workspace, "{}")
                let registeredNamesSet = registry.GetRegisteredNames() |> Set.ofList

                let config = { MaxIterations = 5; MaxRetries = 2; ContextWindowLimit = 10; Model = "test-model" }
                let initialSession : AgentSessionState = {
                    Messages = [ SystemMessage "You are a helpful coding assistant." ]
                    PendingCommand = RunTurn
                }

                let turn1CallCounter = ref 0
                let mockLlmTurn1 : LlmCaller =
                    fun _ _ -> async {
                        incr turn1CallCounter
                        match !turn1CallCounter with
                        | 1 ->
                            let toolCall = {
                                Id = ToolCallId.create "c1" |> Result.defaultWith failwith
                                Name = ToolName.create "write_file" |> Result.defaultWith failwith
                                ArgumentsJson = """{"path":"session.config","content":"mode=dev"}"""
                            }
                            return Ok { Content = ""; ToolCalls = [ toolCall ] }
                        | 2 ->
                            return Ok { Content = "Created session.config in dev mode."; ToolCalls = [] }
                        | _ -> return Error (ApiCallFailed "Unexpected turn 1 call")
                    }

                let! res1, session1 = AgentRunner.runTurnAsync mockLlmTurn1 registry.AsExecutor config "Create session config file." initialSession [] registeredNamesSet
                Expect.equal res1.ApiCalls 2 "Turn 1 should take 2 API calls"
                Expect.equal (File.ReadAllText(Path.Combine(workspace, "session.config"))) "mode=dev" "Turn 1 file content"

                let turn2CallCounter = ref 0
                let mockLlmTurn2 : LlmCaller =
                    fun _ msgs -> async {
                        incr turn2CallCounter
                        match !turn2CallCounter with
                        | 1 ->
                            Expect.isTrue (msgs.Length >= 4) "Turn 2 LLM should receive accumulated history from turn 1"
                            let toolCall = {
                                Id = ToolCallId.create "c2" |> Result.defaultWith failwith
                                Name = ToolName.create "edit_file" |> Result.defaultWith failwith
                                ArgumentsJson = """{"path":"session.config","search":"mode=dev","replace":"mode=prod"}"""
                            }
                            return Ok { Content = ""; ToolCalls = [ toolCall ] }
                        | 2 ->
                            return Ok { Content = "Updated session.config to prod mode."; ToolCalls = [] }
                        | _ -> return Error (ApiCallFailed "Unexpected turn 2 call")
                    }

                let! res2, session2 = AgentRunner.runTurnAsync mockLlmTurn2 registry.AsExecutor config "Change session config mode to prod." session1 [] registeredNamesSet
                Expect.equal res2.ApiCalls 2 "Turn 2 should take 2 API calls"
                Expect.equal (File.ReadAllText(Path.Combine(workspace, "session.config"))) "mode=prod" "Turn 2 file content should be updated"
                Expect.isTrue (session2.Messages.Length >= 7) "Session 2 state must persist accumulated history across turns"
            }

            testAsync "AgentPipeline handles unregistered tool calls with self-correction loop" {
                let workspace = ensureWorkspace ()
                let registry = ToolRegistry()
                registry.Register("write_file", "Writes file", FileTools.writeFileTool workspace, "{}")
                let registeredNamesSet = registry.GetRegisteredNames() |> Set.ofList

                let callCounter = ref 0
                let mockLlmCaller : LlmCaller =
                    fun _ msgs -> async {
                        incr callCounter
                        match !callCounter with
                        | 1 ->
                            let toolCall = {
                                Id = ToolCallId.create "err_1" |> Result.defaultWith failwith
                                Name = ToolName.create "unknown_tool" |> Result.defaultWith failwith
                                ArgumentsJson = "{}"
                            }
                            return Ok { Content = ""; ToolCalls = [ toolCall ] }
                        | 2 ->
                            let lastMsg = msgs |> List.last
                            match lastMsg with
                            | ToolMessage (_, errText) ->
                                Expect.stringContains errText "is not registered" "Error message should report unregistered tool"
                            | _ -> failtest "Expected ToolMessage with error text"

                            let toolCall = {
                                Id = ToolCallId.create "corr_2" |> Result.defaultWith failwith
                                Name = ToolName.create "write_file" |> Result.defaultWith failwith
                                ArgumentsJson = """{"path":"corrected.txt","content":"self correction successful"}"""
                            }
                            return Ok { Content = ""; ToolCalls = [ toolCall ] }
                        | 3 ->
                            return Ok { Content = "Self-corrected and wrote file."; ToolCalls = [] }
                        | _ -> return Error (ApiCallFailed "Unexpected call count")
                    }

                let initialState : TurnState = {
                    Messages = [ SystemMessage "sys"; UserMessage "Test unregistered tool fallback" ]
                    ApiCalls = 0
                    EmptyContentRetries = 0
                    Command = RunTurn
                    Config = { MaxIterations = 5; MaxRetries = 2; ContextWindowLimit = 10; Model = "test-model" }
                }

                let! result = AgentPipeline.runTurnLoop mockLlmCaller registry.AsExecutor [] registeredNamesSet initialState
                match result.Outcome with
                | TurnOutcome.Completed text ->
                    Expect.equal text "Self-corrected and wrote file." "Final response text"
                    Expect.isTrue (File.Exists(Path.Combine(workspace, "corrected.txt"))) "Corrected tool call must execute write_file"
                | outcome -> failtestf "Expected completed outcome, got %A" outcome
            }

            testAsync "AgentPipeline context window protection trims middle history during long tool execution" {
                let workspace = ensureWorkspace ()
                let registry = ToolRegistry()
                registry.Register("write_file", "Writes file", FileTools.writeFileTool workspace, "{}")
                let registeredNamesSet = registry.GetRegisteredNames() |> Set.ofList

                let callCounter = ref 0
                let mockLlmCaller : LlmCaller =
                    fun _ _ -> async {
                        incr callCounter
                        match !callCounter with
                        | 1 ->
                            let toolCall = {
                                Id = ToolCallId.create "c1" |> Result.defaultWith failwith
                                Name = ToolName.create "write_file" |> Result.defaultWith failwith
                                ArgumentsJson = """{"path":"cw.txt","content":"cw test"}"""
                            }
                            return Ok { Content = ""; ToolCalls = [ toolCall ] }
                        | 2 ->
                            return Ok { Content = "Context window protected turn completed."; ToolCalls = [] }
                        | _ -> return Error (ApiCallFailed "Unexpected call count")
                    }

                let longHistory = SystemMessage "System instructions" :: ([ 1 .. 12 ] |> List.map (fun i -> UserMessage (sprintf "msg %d" i)))
                let initialState : TurnState = {
                    Messages = longHistory
                    ApiCalls = 0
                    EmptyContentRetries = 0
                    Command = RunTurn
                    Config = { MaxIterations = 5; MaxRetries = 2; ContextWindowLimit = 5; Model = "test-model" }
                }

                let! result = AgentPipeline.runTurnLoop mockLlmCaller registry.AsExecutor [] registeredNamesSet initialState
                match result.Outcome with
                | TurnOutcome.Completed text ->
                    Expect.equal text "Context window protected turn completed." "Final response text"
                    Expect.equal result.Messages.Head (SystemMessage "System instructions") "System message must be preserved at head"
                | outcome -> failtestf "Expected completed outcome, got %A" outcome
            }

            testAsync "AgentPipeline enforces budget guard on runaway tool calls" {
                let mockLlmCaller : LlmCaller =
                    fun _ _ -> async {
                        let toolCall = {
                            Id = ToolCallId.create "runaway_call" |> Result.defaultWith failwith
                            Name = ToolName.create "write_file" |> Result.defaultWith failwith
                            ArgumentsJson = """{"path":"runaway.txt","content":"runaway"}"""
                        }
                        return Ok { Content = ""; ToolCalls = [ toolCall ] }
                    }

                let registry = ToolRegistry()
                registry.Register("write_file", "Writes file", (fun _ -> async { return "done" }), "{}")
                let registeredNamesSet = registry.GetRegisteredNames() |> Set.ofList

                let initialState : TurnState = {
                    Messages = [ SystemMessage "sys"; UserMessage "Runaway tool call test" ]
                    ApiCalls = 0
                    EmptyContentRetries = 0
                    Command = RunTurn
                    Config = { MaxIterations = 2; MaxRetries = 1; ContextWindowLimit = 10; Model = "test-model" }
                }

                let! result = AgentPipeline.runTurnLoop mockLlmCaller registry.AsExecutor [] registeredNamesSet initialState
                match result.Outcome with
                | TurnOutcome.Failed (FailureReason.BudgetExhausted reason) ->
                    Expect.stringContains reason "Budget exhausted" "Budget guard should stop runaway loop"
                    Expect.equal result.ApiCalls 2 "API calls should equal MaxIterations"
                | outcome -> failtestf "Expected budget failure outcome, got %A" outcome
            }
        ]
