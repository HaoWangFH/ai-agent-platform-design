using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public static class GitTools
    {
        public static async Task<string> GitStatusAsync(string workspaceRoot)
        {
            return await TerminalTool.ExecuteCommandAsync($"git -C \"{workspaceRoot}\" status");
        }

        public static async Task<string> GitCommitAsync(string workspaceRoot, string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var root = doc.RootElement;
                if (!root.TryGetProperty("message", out var msgProp))
                {
                    return "Error: Missing 'message' argument for git_commit.";
                }

                var message = msgProp.GetString()!;
                var addResult = await TerminalTool.ExecuteCommandAsync($"git -C \"{workspaceRoot}\" add .");
                if (addResult.StartsWith("Error")) return addResult;

                var commitResult = await TerminalTool.ExecuteCommandAsync($"git -C \"{workspaceRoot}\" commit -m \"{message}\"");
                return commitResult;
            }
            catch (Exception ex)
            {
                return $"Error executing git_commit: {ex.Message}";
            }
        }

        public static async Task<string> GitPushAsync(string workspaceRoot)
        {
            return await TerminalTool.ExecuteCommandAsync($"git -C \"{workspaceRoot}\" push");
        }
    }
}
