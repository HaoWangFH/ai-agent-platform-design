namespace Skight.AgentPlatform.FSharp.AppHost

open Aspire.Hosting

module Program =

    [<EntryPoint>]
    let main args =
        let builder = DistributedApplication.CreateBuilder(args)
        let _agent = builder.AddProject("fsharp-agent-platform", "../Skight.AgentPlatform.FSharp/Skight.AgentPlatform.FSharp.fsproj")
        let app = builder.Build()
        app.Run()
        0
