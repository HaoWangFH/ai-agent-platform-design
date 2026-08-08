using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Skight.AgentPlatform.Tests
{
    public class McpIntegrationTests
    {
        [Fact]
        public void McpProtocol_CreateRequests_ReturnsValidJsonRpc()
        {
            var initReq = McpProtocol.CreateInitializeRequest(100, "TestClient", "1.0");
            Assert.Contains("\"jsonrpc\":\"2.0\"", initReq);
            Assert.Contains("\"id\":100", initReq);
            Assert.Contains("\"method\":\"initialize\"", initReq);

            var toolsListReq = McpProtocol.CreateToolsListRequest(101);
            Assert.Contains("\"method\":\"tools/list\"", toolsListReq);

            var toolsCallReq = McpProtocol.CreateToolsCallRequest(102, "calculator", "{\"a\":5,\"b\":10}");
            Assert.Contains("\"name\":\"calculator\"", toolsCallReq);
        }

        [Fact]
        public void McpSchemaTranslator_ParseToolsListAndCall()
        {
            var sampleToolsListJson = """
            {
                "jsonrpc": "2.0",
                "id": 1,
                "result": {
                    "tools": [
                        {
                            "name": "sqlite_query",
                            "description": "Execute raw SQL query on SQLite database",
                            "inputSchema": {
                                "type": "object",
                                "properties": {
                                    "query": { "type": "string" }
                                },
                                "required": ["query"]
                            }
                        }
                    ]
                }
            }
            """;

            var (success, schemas, error) = McpSchemaTranslator.ParseToolsListResponse(sampleToolsListJson);
            Assert.True(success, error);
            Assert.Single(schemas);
            Assert.Equal("sqlite_query", schemas[0].Name);
            Assert.Contains("SQLite database", schemas[0].Description);
            Assert.Contains("query", schemas[0].ParametersJson);

            var sampleToolCallJson = """
            {
                "jsonrpc": "2.0",
                "id": 2,
                "result": {
                    "content": [
                        {
                            "type": "text",
                            "text": "Query result: 42 rows returned."
                        }
                    ]
                }
            }
            """;
            var callResult = McpSchemaTranslator.ParseToolCallResponse(sampleToolCallJson);
            Assert.Equal("Query result: 42 rows returned.", callResult);
        }

        [Fact]
        public async Task McpClient_SubprocessIpc_FullLifecycle()
        {
            var shell = Environment.OSVersion.Platform == PlatformID.Win32NT ? "powershell.exe" : "/bin/bash";

            var psScript = """
            $stdin = [Console]::In
            while (($line = $stdin.ReadLine()) -ne $null) {
                if ($line -like '*"method":"initialize"*') {
                    Write-Output '{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2024-11-05","capabilities":{},"serverInfo":{"name":"MockServer","version":"1.0"}}}'
                }
                elseif ($line -like '*"method":"tools/list"*') {
                    Write-Output '{"jsonrpc":"2.0","id":2,"result":{"tools":[{"name":"mock_mcp_tool","description":"Mock tool for spec test","inputSchema":{"type":"object","properties":{"input":{"type":"string"}}}}]}}'
                }
                elseif ($line -like '*"method":"tools/call"*') {
                    Write-Output '{"jsonrpc":"2.0","id":3,"result":{"content":[{"type":"text","text":"Mock MCP execution success"}]}}'
                }
            }
            """;

            var scriptPath = Path.Combine(Path.GetTempPath(), $"mock_mcp_server_{Guid.NewGuid():N}.ps1");
            await File.WriteAllTextAsync(scriptPath, psScript);

            try
            {
                var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"";
                using var mcpClient = new McpClient(shell, args);

                var (initSuccess, initResp) = await mcpClient.InitializeAsync();
                Assert.True(initSuccess, initResp);

                var (listSuccess, schemas, listErr) = await mcpClient.ListToolsAsync();
                Assert.True(listSuccess, listErr);
                Assert.Single(schemas);
                Assert.Equal("mock_mcp_tool", schemas[0].Name);

                var resultText = await mcpClient.CallToolAsync("mock_mcp_tool", "{\"input\":\"test\"}");
                Assert.Equal("Mock MCP execution success", resultText);
            }
            finally
            {
                if (File.Exists(scriptPath))
                {
                    try { File.Delete(scriptPath); } catch { }
                }
            }
        }
    }
}
