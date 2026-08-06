using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public static class AutomationTools
    {
        public static async Task<string> ScheduleTimerAsync(string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var root = doc.RootElement;
                if (!root.TryGetProperty("seconds", out var secProp) || !root.TryGetProperty("prompt", out var promptProp))
                {
                    return "Error: Missing 'seconds' or 'prompt' argument.";
                }

                int seconds = secProp.GetInt32();
                string prompt = promptProp.GetString()!;
                string taskId = Guid.NewGuid().ToString("N").Substring(0, 8);

                _ = Task.Run(async () =>
                {
                    await Task.Delay(seconds * 1000);
                    Console.WriteLine($"\n  [Automation Timer Fired] Task #{taskId}: '{prompt}'");
                });

                return $"Timer task #{taskId} scheduled for {seconds} seconds. Prompt: '{prompt}'";
            }
            catch (Exception ex)
            {
                return $"Error scheduling timer: {ex.Message}";
            }
        }
    }
}
