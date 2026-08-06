using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public static class SystemInfoTool
    {
        public static Task<string> GetSystemInfoAsync(string workspaceRoot)
        {
            try
            {
                var info = new
                {
                    os = RuntimeInformation.OSDescription,
                    architecture = RuntimeInformation.OSArchitecture.ToString(),
                    framework = RuntimeInformation.FrameworkDescription,
                    machine = Environment.MachineName,
                    user = Environment.UserName,
                    processors = Environment.ProcessorCount,
                    workspace = workspaceRoot,
                    currentDir = Directory.GetCurrentDirectory()
                };

                return Task.FromResult(JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Error fetching system info: {ex.Message}");
            }
        }
    }
}
