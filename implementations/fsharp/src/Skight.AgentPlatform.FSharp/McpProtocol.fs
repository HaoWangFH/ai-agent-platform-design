namespace Skight.AgentPlatform.FSharp

open System
open System.Text.Json
open System.Text.Json.Serialization

module McpProtocol =

    [<CLIMutable>]
    type JsonRpcRequest = {
        [<JsonPropertyName("jsonrpc")>]
        JsonRpc: string
        [<JsonPropertyName("id")>]
        Id: int64
        [<JsonPropertyName("method")>]
        Method: string
        [<JsonPropertyName("params")>]
        Params: obj
    }

    let createRequest (id: int64) (methodName: string) (paramsObj: obj) : string =
        let req = {
            JsonRpc = "2.0"
            Id = id
            Method = methodName
            Params = if isNull paramsObj then obj() else paramsObj
        }
        JsonSerializer.Serialize(req)

    let createInitializeRequest (id: int64) (clientName: string) (clientVersion: string) : string =
        let paramsObj = {|
            protocolVersion = "2024-11-05"
            capabilities = {| tools = {||} |}
            clientInfo = {|
                name = clientName
                version = clientVersion
            |}
        |}
        createRequest id "initialize" paramsObj

    let createToolsListRequest (id: int64) : string =
        createRequest id "tools/list" {| |}

    let createToolsCallRequest (id: int64) (toolName: string) (argumentsJson: string) : string =
        let parsedArgs =
            if String.IsNullOrWhiteSpace(argumentsJson) then
                JsonDocument.Parse("{}").RootElement
            else
                try JsonDocument.Parse(argumentsJson).RootElement
                with _ -> JsonDocument.Parse("{}").RootElement

        let paramsObj = {|
            name = toolName
            arguments = parsedArgs
        |}
        createRequest id "tools/call" paramsObj
