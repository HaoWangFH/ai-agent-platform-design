using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public static class DelegateTool
    {
        public static async Task<string> DelegateTaskAsync(AgentConfig parentConfig, ToolRegistry registry, string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var root = doc.RootElement;
                if (!root.TryGetProperty("role", out var roleProp) || !root.TryGetProperty("task", out var taskProp))
                {
                    return "Error: Missing 'role' or 'task' argument for delegate_task.";
                }

                var role = roleProp.GetString()!;
                var taskDescription = taskProp.GetString()!;

                // Clone config for subagent
                var subConfig = new AgentConfig
                {
                    ApiKey = parentConfig.ApiKey,
                    Model = parentConfig.Model,
                    Endpoint = parentConfig.Endpoint,
                    JwtToken = parentConfig.JwtToken,
                    MaxIterations = 5 // Cap subagent depth/iterations to prevent infinite recursion
                };

                var subRunner = new AgentRunner(subConfig, registry);
                var prompt = $"You are a specialized subagent acting as: {role}. Your sole objective is: {taskDescription}";
                
                var result = await subRunner.RunAsync(prompt);
                return $"Subagent ({role}) output: {result.FinalResponse}";
            }
            catch (Exception ex)
            {
                return $"Error executing delegate_task: {ex.Message}";
            }
        }
    }
}
