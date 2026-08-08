using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Skight.AgentPlatform
{
    public class McpToolSchema
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ParametersJson { get; set; } = "{\"type\":\"object\",\"properties\":{}}";
    }

    public static class McpSchemaTranslator
    {
        public static (bool Success, List<McpToolSchema> Schemas, string Error) ParseToolsListResponse(string jsonResponse)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var errorElem) && errorElem.ValueKind != JsonValueKind.Null)
                {
                    var errMsg = errorElem.TryGetProperty("message", out var msgElem) ? msgElem.GetString() ?? "Unknown JSON-RPC error" : "Unknown JSON-RPC error";
                    return (false, new List<McpToolSchema>(), $"MCP Server returned error: {errMsg}");
                }

                if (!root.TryGetProperty("result", out var resultElem))
                {
                    return (false, new List<McpToolSchema>(), "Invalid MCP response: Missing 'result' property");
                }

                var list = new List<McpToolSchema>();
                if (resultElem.TryGetProperty("tools", out var toolsElem) && toolsElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var t in toolsElem.EnumerateArray())
                    {
                        var nameStr = t.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var descStr = t.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                        var schemaJson = t.TryGetProperty("inputSchema", out var s) ? s.GetRawText() : "{\"type\":\"object\",\"properties\":{}}";

                        if (!string.IsNullOrWhiteSpace(nameStr))
                        {
                            list.Add(new McpToolSchema
                            {
                                Name = nameStr,
                                Description = descStr,
                                ParametersJson = schemaJson
                            });
                        }
                    }
                }

                return (true, list, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, new List<McpToolSchema>(), $"Failed to parse MCP tools/list response: {ex.Message}");
            }
        }

        public static string ParseToolCallResponse(string jsonResponse)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var errorElem) && errorElem.ValueKind != JsonValueKind.Null)
                {
                    var errMsg = errorElem.TryGetProperty("message", out var msgElem) ? msgElem.GetString() ?? "Unknown JSON-RPC error" : "Unknown JSON-RPC error";
                    return $"Error from MCP server: {errMsg}";
                }

                if (!root.TryGetProperty("result", out var resultElem))
                {
                    return $"Error from MCP server: Invalid response structure: {jsonResponse}";
                }

                var isError = resultElem.TryGetProperty("isError", out var errFlag) && errFlag.GetBoolean();

                if (resultElem.TryGetProperty("content", out var contentElem) && contentElem.ValueKind == JsonValueKind.Array)
                {
                    var texts = new List<string>();
                    foreach (var c in contentElem.EnumerateArray())
                    {
                        if (c.TryGetProperty("text", out var textElem))
                        {
                            texts.Add(textElem.GetString() ?? "");
                        }
                    }
                    var combined = string.Join("\n", texts);
                    return isError ? $"Error executing MCP tool:\n{combined}" : combined;
                }

                return resultElem.GetRawText();
            }
            catch (Exception ex)
            {
                return $"Error parsing MCP tool response: {ex.Message}";
            }
        }
    }
}
