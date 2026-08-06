using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public static class MemoryTool
    {
        private static readonly ConcurrentDictionary<string, string> Store = new();

        public static Task<string> StoreMemoryAsync(string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var root = doc.RootElement;
                if (!root.TryGetProperty("key", out var keyProp) || !root.TryGetProperty("value", out var valProp))
                {
                    return Task.FromResult("Error: Missing 'key' or 'value' argument.");
                }

                var key = keyProp.GetString()!;
                var value = valProp.GetString()!;
                Store[key] = value;
                return Task.FromResult($"Memory stored for key '{key}'.");
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Error storing memory: {ex.Message}");
            }
        }

        public static Task<string> RecallMemoryAsync(string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var root = doc.RootElement;
                if (!root.TryGetProperty("key", out var keyProp))
                {
                    return Task.FromResult("Error: Missing 'key' argument.");
                }

                var key = keyProp.GetString()!;
                if (Store.TryGetValue(key, out var val))
                {
                    return Task.FromResult($"Memory '{key}': {val}");
                }
                return Task.FromResult($"Memory '{key}' not found.");
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Error recalling memory: {ex.Message}");
            }
        }
    }
}
