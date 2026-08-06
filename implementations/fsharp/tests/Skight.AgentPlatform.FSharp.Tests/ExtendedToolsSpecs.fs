namespace Skight.AgentPlatform.FSharp.Tests

open System.IO
open System.Text.Json
open Expecto
open Skight.AgentPlatform.FSharp

module ExtendedToolsSpecs =

    [<Tests>]
    let tests =
        testList "Media and Automation Extended Tools Specs" [

            testAsync "Feature: Inspect Image File and Return Data URI" {
                let workspace = Directory.GetCurrentDirectory()
                let imgPath = "sample_test.png"
                let fullPath = Path.Combine(workspace, imgPath)
                File.WriteAllBytes(fullPath, [| 0x89uy; 0x50uy; 0x4Euy; 0x47uy |])
                let jsonArg = JsonSerializer.Serialize({| path = imgPath |})

                let! result = MediaTools.inspectImageAsync workspace jsonArg |> Async.AwaitTask
                if File.Exists(fullPath) then File.Delete(fullPath)

                Expect.stringContains result "image/png" "Response should contain MIME type"
                Expect.stringContains result "data:image/png;base64" "Response should contain base64 URI"
            }

            testAsync "Feature: Inspect Audio File and Return Data URI" {
                let workspace = Directory.GetCurrentDirectory()
                let audioPath = "sample_test.mp3"
                let fullPath = Path.Combine(workspace, audioPath)
                File.WriteAllBytes(fullPath, [| 0x49uy; 0x44uy; 0x33uy |])
                let jsonArg = JsonSerializer.Serialize({| path = audioPath |})

                let! result = MediaTools.inspectAudioAsync workspace jsonArg |> Async.AwaitTask
                if File.Exists(fullPath) then File.Delete(fullPath)

                Expect.stringContains result "audio/mp3" "Response should contain MIME type"
                Expect.stringContains result "data:audio/mp3;base64" "Response should contain base64 URI"
            }

            testAsync "Feature: Transcribe Audio Speech to Text" {
                let workspace = Directory.GetCurrentDirectory()
                let audioPath = "transcribe_test.mp3"
                let fullPath = Path.Combine(workspace, audioPath)
                File.WriteAllBytes(fullPath, [| 0x49uy; 0x44uy; 0x33uy |])
                let jsonArg = JsonSerializer.Serialize({| path = audioPath |})

                let! result = MediaTools.transcribeAudioAsync workspace jsonArg |> Async.AwaitTask
                if File.Exists(fullPath) then File.Delete(fullPath)

                Expect.stringContains result "Audio Transcription Stub" "Response should contain transcription confirmation"
                Expect.stringContains result audioPath "Response should mention audio path"
            }

            testAsync "Feature: Synthesize Text to Speech Audio" {
                let workspace = Directory.GetCurrentDirectory()
                let outPath = "speech_test.mp3"
                let fullPath = Path.Combine(workspace, outPath)
                let jsonArg = JsonSerializer.Serialize({| text = "Hello Expecto!"; output_path = outPath |})

                let! result = MediaTools.textToSpeechAsync workspace jsonArg |> Async.AwaitTask
                let fileCreated = File.Exists(fullPath)
                if fileCreated then File.Delete(fullPath)

                Expect.isTrue fileCreated "Audio output file should be generated on disk"
                Expect.stringContains result "Text-to-Speech audio generated" "Response should confirm synthesis"
            }

            testAsync "Feature: Schedule Automation Timer Task" {
                let jsonArg = JsonSerializer.Serialize({| seconds = 1; prompt = "Perform backup" |})
                let! result = AutomationTools.scheduleTimerAsync jsonArg |> Async.AwaitTask
                Expect.stringContains result "Timer task #" "Confirmation should state task ID"
                Expect.stringContains result "Perform backup" "Confirmation should state prompt"
            }
        ]
