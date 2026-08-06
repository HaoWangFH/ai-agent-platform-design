namespace Skight.AgentPlatform.FSharp

open System
open System.IO
open System.Runtime.InteropServices
open System.Text.Json
open System.Threading.Tasks

module SystemInfoTool =

    let getSystemInfoAsync (workspaceRoot: string) : Task<string> =
        Task.FromResult(
            try
                let info = {|
                    os = RuntimeInformation.OSDescription
                    architecture = RuntimeInformation.OSArchitecture.ToString()
                    framework = RuntimeInformation.FrameworkDescription
                    machine = Environment.MachineName
                    user = Environment.UserName
                    processors = Environment.ProcessorCount
                    workspace = workspaceRoot
                    currentDir = Directory.GetCurrentDirectory()
                |}

                let options = JsonSerializerOptions(WriteIndented = true)
                JsonSerializer.Serialize(info, options)
            with ex -> sprintf "Error fetching system info: %s" ex.Message
        )
