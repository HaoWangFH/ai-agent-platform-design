namespace Skight.AgentPlatform.FSharp.AppHost

open Aspire.Hosting

module Program =

    [<EntryPoint>]
    let main args =
        let builder = DistributedApplication.CreateBuilder(args)
        let app = builder.Build()
        app.Run()
        0
