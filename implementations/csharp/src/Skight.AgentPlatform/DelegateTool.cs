using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public static class DelegateTool
    {
        private static async Task<string> RunSingleSubAgentAsync(AgentConfig parentConfig, ToolRegistry registry, string role, string taskDescription)
        {
            var subConfig = new AgentConfig
            {
                ApiKey = parentConfig.ApiKey,
                Model = parentConfig.Model,
                Endpoint = parentConfig.Endpoint,
                JwtToken = parentConfig.JwtToken,
                MaxIterations = 5
            };

            var subRunner = new AgentRunner(subConfig, registry);
            var prompt = $"You are a specialized subagent acting as: {role}. Your sole objective is: {taskDescription}";
            
            var result = await subRunner.RunAsync(prompt);
            return $"Subagent ({role}) output: {result.FinalResponse}";
        }

        public static async Task<string> DelegateTaskAsync(AgentConfig parentConfig, ToolRegistry registry, string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("tasks", out var tasksElem) && tasksElem.ValueKind == JsonValueKind.Array)
                {
                    var tasks = new List<Task<string>>();
                    foreach (var t in tasksElem.EnumerateArray())
                    {
                        var roleStr = t.TryGetProperty("role", out var r) ? r.GetString() ?? "leaf" : "leaf";
                        var taskStr = t.TryGetProperty("task", out var tk) ? tk.GetString() ?? "" :
                                      t.TryGetProperty("goal", out var g) ? g.GetString() ?? "" : "";

                        if (!string.IsNullOrWhiteSpace(taskStr))
                        {
                            tasks.Add(RunSingleSubAgentAsync(parentConfig, registry, roleStr, taskStr));
                        }
                    }

                    if (tasks.Count == 0)
                    {
                        return "Error: Empty 'tasks' array provided for delegate_task.";
                    }

                    var results = await Task.WhenAll(tasks);
                    return $"Batch Subagent Execution Results:\n{string.Join("\n---\n", results)}";
                }
                else if (root.TryGetProperty("task", out var taskElem) || root.TryGetProperty("goal", out taskElem))
                {
                    var taskDescription = taskElem.GetString()!;
                    var role = root.TryGetProperty("role", out var roleProp) ? roleProp.GetString() ?? "leaf" : "leaf";
                    return await RunSingleSubAgentAsync(parentConfig, registry, role, taskDescription);
                }
                else
                {
                    return "Error: Missing 'task', 'goal', or 'tasks' argument for delegate_task.";
                }
            }
            catch (Exception ex)
            {
                return $"Error executing delegate_task: {ex.Message}";
            }
        }
    }
}
