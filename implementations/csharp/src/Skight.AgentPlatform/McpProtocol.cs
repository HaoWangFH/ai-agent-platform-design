using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Skight.AgentPlatform
{
    public class JsonRpcRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public object Params { get; set; } = new { };
    }

    public static class McpProtocol
    {
        public static string CreateRequest(long id, string methodName, object paramsObj)
        {
            var req = new JsonRpcRequest
            {
                JsonRpc = "2.0",
                Id = id,
                Method = methodName,
                Params = paramsObj ?? new { }
            };
            return JsonSerializer.Serialize(req);
        }

        public static string CreateInitializeRequest(long id, string clientName = "Skight.AgentPlatform", string clientVersion = "1.0.0")
        {
            var paramsObj = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { tools = new { } },
                clientInfo = new
                {
                    name = clientName,
                    version = clientVersion
                }
            };
            return CreateRequest(id, "initialize", paramsObj);
        }

        public static string CreateToolsListRequest(long id)
        {
            return CreateRequest(id, "tools/list", new { });
        }

        public static string CreateToolsCallRequest(long id, string toolName, string argumentsJson)
        {
            JsonElement parsedArgs;
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                parsedArgs = JsonDocument.Parse("{}").RootElement;
            }
            else
            {
                try
                {
                    parsedArgs = JsonDocument.Parse(argumentsJson).RootElement;
                }
                catch
                {
                    parsedArgs = JsonDocument.Parse("{}").RootElement;
                }
            }

            var paramsObj = new
            {
                name = toolName,
                arguments = parsedArgs
            };
            return CreateRequest(id, "tools/call", paramsObj);
        }
    }
}
