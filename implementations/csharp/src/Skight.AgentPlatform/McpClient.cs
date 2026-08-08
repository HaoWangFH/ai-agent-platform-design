using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public class McpClient : IDisposable
    {
        private readonly Process _proc;
        private long _nextReqId = 1L;
        private bool _isDisposed;

        public McpClient(string command, string args)
        {
            _proc = new Process();
            _proc.StartInfo.FileName = command;
            _proc.StartInfo.Arguments = args;
            _proc.StartInfo.RedirectStandardInput = true;
            _proc.StartInfo.RedirectStandardOutput = true;
            _proc.StartInfo.RedirectStandardError = true;
            _proc.StartInfo.UseShellExecute = false;
            _proc.StartInfo.CreateNoWindow = true;

            try
            {
                if (!_proc.Start())
                {
                    throw new InvalidOperationException($"Failed to start MCP server process '{command} {args}'");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to launch MCP server process '{command} {args}': {ex.Message}", ex);
            }
        }

        private long GetNextId()
        {
            return Interlocked.Increment(ref _nextReqId);
        }

        public async Task<string> SendRequestAsync(string reqJson, int timeoutMs = 30000)
        {
            if (_proc.HasExited)
            {
                return $"{{\"jsonrpc\":\"2.0\",\"id\":0,\"error\":{{\"code\":-32000,\"message\":\"MCP server process exited with code {_proc.ExitCode}\"}}}}";
            }

            try
            {
                await _proc.StandardInput.WriteLineAsync(reqJson);
                await _proc.StandardInput.FlushAsync();

                var readTask = _proc.StandardOutput.ReadLineAsync();
                var completed = await Task.WhenAny(readTask, Task.Delay(timeoutMs));

                if (completed == readTask)
                {
                    var respLine = await readTask;
                    if (respLine == null)
                    {
                        return "{\"jsonrpc\":\"2.0\",\"id\":0,\"error\":{\"code\":-32000,\"message\":\"MCP server closed stdout stream\"}}";
                    }
                    return respLine;
                }
                else
                {
                    return $"{{\"jsonrpc\":\"2.0\",\"id\":0,\"error\":{{\"code\":-32000,\"message\":\"MCP server request timed out after {timeoutMs} ms\"}}}}";
                }
            }
            catch (Exception ex)
            {
                return $"{{\"jsonrpc\":\"2.0\",\"id\":0,\"error\":{{\"code\":-32000,\"message\":\"IPC communication error: {ex.Message}\"}}}}";
            }
        }

        public async Task<(bool Success, string ResponseOrError)> InitializeAsync(string clientName = "Skight.AgentPlatform", string clientVersion = "1.0.0")
        {
            var id = GetNextId();
            var reqJson = McpProtocol.CreateInitializeRequest(id, clientName, clientVersion);
            var respJson = await SendRequestAsync(reqJson);

            if (respJson.Contains("\"error\"") && !respJson.Contains("\"result\""))
            {
                return (false, $"MCP Initialization failed: {respJson}");
            }
            return (true, respJson);
        }

        public async Task<(bool Success, List<McpToolSchema> Schemas, string Error)> ListToolsAsync()
        {
            var id = GetNextId();
            var reqJson = McpProtocol.CreateToolsListRequest(id);
            var respJson = await SendRequestAsync(reqJson);
            return McpSchemaTranslator.ParseToolsListResponse(respJson);
        }

        public async Task<string> CallToolAsync(string toolName, string argumentsJson)
        {
            var id = GetNextId();
            var reqJson = McpProtocol.CreateToolsCallRequest(id, toolName, argumentsJson);
            var respJson = await SendRequestAsync(reqJson);
            return McpSchemaTranslator.ParseToolCallResponse(respJson);
        }

        public async Task<(bool Success, int RegisteredCount, string Error)> RegisterWithRegistryAsync(ToolRegistry registry, string prefix = "")
        {
            var initResult = await InitializeAsync();
            if (!initResult.Success)
            {
                return (false, 0, initResult.ResponseOrError);
            }

            var listResult = await ListToolsAsync();
            if (!listResult.Success)
            {
                return (false, 0, listResult.Error);
            }

            int count = 0;
            foreach (var schema in listResult.Schemas)
            {
                var finalName = string.IsNullOrEmpty(prefix) ? schema.Name : $"{prefix}_{schema.Name}";
                registry.Register(finalName, schema.Description, argsJson => CallToolAsync(schema.Name, argsJson), schema.ParametersJson);
                count++;
            }

            return (true, count, string.Empty);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                try
                {
                    if (!_proc.HasExited)
                    {
                        _proc.Kill(true);
                    }
                }
                catch { }
                _proc.Dispose();
            }
        }
    }
}
