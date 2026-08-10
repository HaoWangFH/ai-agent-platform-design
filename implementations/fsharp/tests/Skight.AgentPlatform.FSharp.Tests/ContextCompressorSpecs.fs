namespace Skight.AgentPlatform.FSharp.Tests

open Expecto
open Skight.AgentPlatform.FSharp

module ContextCompressorSpecs =

    [<Tests>]
    let tests =
        testList "Context Compaction Engine Specs (context_compressor)" [

            test "Feature: Trigger context compaction when history exceeds 80% threshold" {
                let systemMsg = SystemMessage "system prompt"
                let userMsgs = [ 1 .. 10 ] |> List.map (fun i -> UserMessage (sprintf "user message %d" i))
                let msgs = systemMsg :: userMsgs // 11 messages total

                // Limit = 10, Threshold = 8 (80%) -> 11 > 8, should compact
                let compressed = ContextCompressor.compress 0.80 10 msgs

                Expect.isTrue (compressed.Length < msgs.Length) "Compressed list should be shorter than original"
                Expect.equal compressed.Head systemMsg "System prompt preserved at index 0"

                match compressed.[1] with
                | SystemMessage text -> Expect.stringContains text "[TURN SUMMARY]" "Index 1 contains [TURN SUMMARY]"
                | _ -> failwith "Expected SystemMessage with turn summary"
            }
        ]
