namespace Skight.AgentPlatform.FSharp

open System
open System.Text.Json

module McpSchemaTranslator =

    /// Parses an MCP tools/list JSON-RPC response into a list of ToolSchema records
    let parseToolsListResponse (jsonResponse: string) : Result<ToolSchema list, string> =
        try
            use doc = JsonDocument.Parse(jsonResponse)
            let root = doc.RootElement
            let mutable errorElement = JsonElement()
            let mutable resultElement = JsonElement()

            if root.TryGetProperty("error", &errorElement) && errorElement.ValueKind <> JsonValueKind.Null then
                let mutable msgElement = JsonElement()
                let errMsg = if errorElement.TryGetProperty("message", &msgElement) then msgElement.GetString() else "Unknown JSON-RPC error"
                Error (sprintf "MCP Server returned error: %s" errMsg)
            elif not (root.TryGetProperty("result", &resultElement)) then
                Error "Invalid MCP response: Missing 'result' property"
            else
                let mutable toolsElement = JsonElement()
                if not (resultElement.TryGetProperty("tools", &toolsElement)) then
                    Ok []
                else
                    let schemas = [
                        for t in toolsElement.EnumerateArray() do
                            let mutable nameElem = JsonElement()
                            let mutable descElem = JsonElement()
                            let mutable schemaElem = JsonElement()

                            let nameStr = if t.TryGetProperty("name", &nameElem) then nameElem.GetString() else ""
                            let descStr = if t.TryGetProperty("description", &descElem) then descElem.GetString() else ""
                            let inputSchemaJson =
                                if t.TryGetProperty("inputSchema", &schemaElem) then
                                    schemaElem.GetRawText()
                                else
                                    """{"type":"object","properties":{}}"""

                            match ToolName.create nameStr with
                            | Ok name ->
                                yield {
                                    Name = name
                                    Description = descStr
                                    ParametersJson = inputSchemaJson
                                }
                            | Error _ -> ()
                    ]
                    Ok schemas
        with ex ->
            Error (sprintf "Failed to parse MCP tools/list response: %s" ex.Message)

    /// Parses an MCP tools/call JSON-RPC response into an execution result string
    let parseToolCallResponse (jsonResponse: string) : string =
        try
            use doc = JsonDocument.Parse(jsonResponse)
            let root = doc.RootElement
            let mutable errorElement = JsonElement()
            let mutable resultElement = JsonElement()

            if root.TryGetProperty("error", &errorElement) && errorElement.ValueKind <> JsonValueKind.Null then
                let mutable msgElement = JsonElement()
                let errMsg = if errorElement.TryGetProperty("message", &msgElement) then msgElement.GetString() else "Unknown JSON-RPC error"
                sprintf "Error from MCP server: %s" errMsg
            elif not (root.TryGetProperty("result", &resultElement)) then
                sprintf "Error from MCP server: Invalid response structure: %s" jsonResponse
            else
                let mutable isErrElem = JsonElement()
                let mutable contentElem = JsonElement()

                let isError = resultElement.TryGetProperty("isError", &isErrElem) && isErrElem.GetBoolean()

                if resultElement.TryGetProperty("content", &contentElem) then
                    let textList = [
                        for c in contentElem.EnumerateArray() do
                            let mutable textElem = JsonElement()
                            if c.TryGetProperty("text", &textElem) then
                                yield textElem.GetString()
                    ]
                    let combined = String.concat "\n" textList
                    if isError then sprintf "Error executing MCP tool:\n%s" combined
                    else combined
                else
                    resultElement.GetRawText()
        with ex ->
            sprintf "Error parsing MCP tool response: %s" ex.Message
