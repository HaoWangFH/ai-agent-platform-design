using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace Skight.AgentPlatform
{
    public static class Tools
    {
        public static void RegisterMockTools(ToolRegistry registry, string specDirPath)
        {
            var mockToolsJsonPath = Path.Combine(specDirPath, "mock_tools.json");
            var jsonString = File.ReadAllText(mockToolsJsonPath);
            using var document = JsonDocument.Parse(jsonString);
            var toolsArray = document.RootElement.GetProperty("tools");

            foreach (var tool in toolsArray.EnumerateArray())
            {
                var name = tool.GetProperty("name").GetString()!;
                var description = tool.GetProperty("description").GetString()!;
                var parameters = tool.GetProperty("parameters").GetRawText();

                Func<string, Task<string>> handler = argsJson =>
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(argsJson);
                        var root = doc.RootElement;

                        if (name == "get_weather")
                        {
                            var location = root.TryGetProperty("location", out var locProp) ? locProp.GetString() : "unknown";
                            var unit = root.TryGetProperty("unit", out var uProp) ? uProp.GetString() : "celsius";
                            if (location?.ToLower().Contains("san francisco") == true)
                            {
                                return Task.FromResult($"The weather in {location} is 16 degrees {unit} and foggy.");
                            }
                            return Task.FromResult($"The weather in {location} is 22 degrees {unit} and sunny.");
                        }

                        if (name == "read_file")
                        {
                            var path = root.GetProperty("path").GetString()!;
                            if (File.Exists(path))
                            {
                                return Task.FromResult(File.ReadAllText(path));
                            }
                            return Task.FromResult($"Error: File '{path}' not found.");
                        }

                        return Task.FromResult($"Tool '{name}' executed successfully with args: {argsJson}");
                    }
                    catch (Exception ex)
                    {
                        return Task.FromResult($"Error executing tool '{name}': {ex.Message}");
                    }
                };

                registry.Register(name, description, handler, parameters);
            }
        }
        public static void RegisterCoreTools(ToolRegistry registry, string workspaceRoot)
        {
            registry.Register("read_file", "Read contents of a file.",
                argsJson => FileTools.ReadFileAsync(workspaceRoot, argsJson),
                @"{""type"":""object"",""properties"":{""path"":{""type"":""string""}},""required"":[""path""]}");

            registry.Register("write_file", "Write contents to a file.",
                argsJson => FileTools.WriteFileAsync(workspaceRoot, argsJson),
                @"{""type"":""object"",""properties"":{""path"":{""type"":""string""},""content"":{""type"":""string""}},""required"":[""path"",""content""]}");

            registry.Register("edit_file", "Edit a file using search/replace or patch.",
                argsJson => FileTools.EditFileAsync(workspaceRoot, argsJson),
                @"{""type"":""object"",""properties"":{""path"":{""type"":""string""},""search"":{""type"":""string""},""replace"":{""type"":""string""},""patch"":{""type"":""string""}},""required"":[""path""]}");

            registry.Register("execute_command", TerminalTool.GetToolDescription(),
                argsJson =>
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(argsJson);
                        if (doc.RootElement.TryGetProperty("command", out var cmdProp))
                        {
                            return TerminalTool.ExecuteCommandAsync(cmdProp.GetString()!);
                        }
                        return Task.FromResult("Error: Missing 'command' argument.");
                    }
                    catch (Exception ex) { return Task.FromResult($"Error: {ex.Message}"); }
                },
                @"{""type"":""object"",""properties"":{""command"":{""type"":""string""}},""required"":[""command""]}");
                
            registry.Register("start_background_command", "Start a long running background command.",
                argsJson =>
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(argsJson);
                        if (doc.RootElement.TryGetProperty("command", out var cmdProp))
                        {
                            var id = TerminalTool.StartBackgroundCommand(cmdProp.GetString()!);
                            return Task.FromResult($"Background command started with ID: {id}");
                        }
                        return Task.FromResult("Error: Missing 'command' argument.");
                    }
                    catch (Exception ex) { return Task.FromResult($"Error: {ex.Message}"); }
                },
                @"{""type"":""object"",""properties"":{""command"":{""type"":""string""}},""required"":[""command""]}");

            registry.Register("get_background_command_output", "Get output of a background command.",
                argsJson =>
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(argsJson);
                        if (doc.RootElement.TryGetProperty("id", out var idProp))
                        {
                            return Task.FromResult(TerminalTool.GetBackgroundCommandOutput(idProp.GetString()!));
                        }
                        return Task.FromResult("Error: Missing 'id' argument.");
                    }
                    catch (Exception ex) { return Task.FromResult($"Error: {ex.Message}"); }
                },
                @"{""type"":""object"",""properties"":{""id"":{""type"":""string""}},""required"":[""id""]}");

            registry.Register("git_status", "Get the git status of the workspace.",
                argsJson => GitTools.GitStatusAsync(workspaceRoot),
                @"{""type"":""object"",""properties"":{}}");

            registry.Register("git_commit", "Stage and commit changes in workspace.",
                argsJson => GitTools.GitCommitAsync(workspaceRoot, argsJson),
                @"{""type"":""object"",""properties"":{""message"":{""type"":""string""}},""required"":[""message""]}");

            registry.Register("git_push", "Push committed changes to origin.",
                argsJson => GitTools.GitPushAsync(workspaceRoot),
                @"{""type"":""object"",""properties"":{}}");

            registry.Register("web_fetch_content", "Fetch text content from a web URL.",
                argsJson => WebTools.FetchUrlContentAsync(argsJson),
                @"{""type"":""object"",""properties"":{""url"":{""type"":""string""}},""required"":[""url""]}");

            registry.Register("store_memory", "Store a key-value memory.",
                argsJson => MemoryTool.StoreMemoryAsync(argsJson),
                @"{""type"":""object"",""properties"":{""key"":{""type"":""string""},""value"":{""type"":""string""}},""required"":[""key"",""value""]}");

            registry.Register("recall_memory", "Recall a stored memory by key.",
                argsJson => MemoryTool.RecallMemoryAsync(argsJson),
                @"{""type"":""object"",""properties"":{""key"":{""type"":""string""}},""required"":[""key""]}");

            registry.Register("add_todo", "Add a task to the session TODO list.",
                argsJson => TodoTool.AddTodoAsync(argsJson),
                @"{""type"":""object"",""properties"":{""task"":{""type"":""string""}},""required"":[""task""]}");

            registry.Register("list_todos", "List all session TODO tasks.",
                argsJson => TodoTool.ListTodosAsync(),
                @"{""type"":""object"",""properties"":{}}");

            registry.Register("complete_todo", "Mark a session TODO task as complete.",
                argsJson => TodoTool.CompleteTodoAsync(argsJson),
                @"{""type"":""object"",""properties"":{""id"":{""type"":""integer""}},""required"":[""id""]}");
        }

        public static void RegisterDelegateTool(ToolRegistry registry, AgentConfig config)
        {
            registry.Register("delegate_task", "Delegate a subtask to an autonomous subagent.",
                argsJson => DelegateTool.DelegateTaskAsync(config, registry, argsJson),
                @"{""type"":""object"",""properties"":{""role"":{""type"":""string""},""task"":{""type"":""string""}},""required"":[""role"",""task""]}");
        }
    }
}
