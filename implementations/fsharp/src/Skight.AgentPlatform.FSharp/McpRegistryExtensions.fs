namespace Skight.AgentPlatform.FSharp

open System

[<AutoOpen>]
module McpRegistryExtensions =

    type ToolRegistry with
        /// Dynamically discovers and registers all tools provided by an McpClient instance
        member this.RegisterMcpClientAsync(mcpClient: McpClient, ?prefix: string) : Async<Result<int, string>> =
            async {
                match! mcpClient.InitializeAsync() with
                | Error err -> return Error (sprintf "Failed to initialize MCP client: %s" err)
                | Ok _ ->
                    match! mcpClient.ListToolsAsync() with
                    | Error err -> return Error (sprintf "Failed to list MCP tools: %s" err)
                    | Ok schemas ->
                        let pfx = defaultArg prefix ""
                        let mutable registeredCount = 0

                        for schema in schemas do
                            let rawName = ToolName.value schema.Name
                            let finalNameStr = if String.IsNullOrEmpty pfx then rawName else sprintf "%s_%s" pfx rawName

                            match ToolName.create finalNameStr with
                            | Error err ->
                                printfn "  [MCP Registration Warning] Skipping invalid tool name '%s': %s" finalNameStr err
                            | Ok finalName ->
                                let handler (argsJson: string) : Async<string> =
                                    mcpClient.CallToolAsync(schema.Name, argsJson)

                                this.Register(ToolName.value finalName, schema.Description, handler, schema.ParametersJson)
                                registeredCount <- registeredCount + 1

                        return Ok registeredCount
            }
