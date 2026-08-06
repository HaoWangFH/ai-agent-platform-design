using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public static class GitTools
    {
        public static async Task<string> GitStatusAsync(string workspaceRoot)
        {
            var path = workspaceRoot.Replace('\\', '/');
            return await TerminalTool.ExecuteCommandAsync($"git -C \"{path}\" status");
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
                var path = workspaceRoot.Replace('\\', '/');
                var addResult = await TerminalTool.ExecuteCommandAsync($"git -C \"{path}\" add .");
                if (addResult.StartsWith("Error")) return addResult;

                var commitResult = await TerminalTool.ExecuteCommandAsync($"git -C \"{path}\" commit -m \"{message}\"");
                return commitResult;
            }
            catch (Exception ex)
            {
                return $"Error executing git_commit: {ex.Message}";
            }
        }

        public static async Task<string> GitPushAsync(string workspaceRoot)
        {
            var path = workspaceRoot.Replace('\\', '/');
            return await TerminalTool.ExecuteCommandAsync($"git -C \"{path}\" push");
        }

        public static async Task<string> GitDiffAsync(string workspaceRoot)
        {
            var path = workspaceRoot.Replace('\\', '/');
            return await TerminalTool.ExecuteCommandAsync($"git -C \"{path}\" diff");
        }

        public static async Task<string> GitLogAsync(string workspaceRoot, string argsJson)
        {
            int count = 5;
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
                if (doc.RootElement.TryGetProperty("count", out var countProp))
                {
                    count = countProp.GetInt32();
                }
            }
            catch { }

            var path = workspaceRoot.Replace('\\', '/');
            return await TerminalTool.ExecuteCommandAsync($"git -C \"{path}\" log -n {count} --oneline");
        }
    }
}
