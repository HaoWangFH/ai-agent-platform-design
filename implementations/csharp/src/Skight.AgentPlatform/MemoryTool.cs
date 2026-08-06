using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public static class MemoryTool
    {
        private static readonly ConcurrentDictionary<string, string> Store = new();
        private static readonly string MemoryFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".agent_memory.json");

        static MemoryTool()
        {
            LoadFromDisk();
        }

        private static void LoadFromDisk()
        {
            try
            {
                if (File.Exists(MemoryFilePath))
                {
                    var json = File.ReadAllText(MemoryFilePath);
                    var dictionary = JsonSerializer.Deserialize<ConcurrentDictionary<string, string>>(json);
                    if (dictionary != null)
                    {
                        foreach (var kvp in dictionary)
                        {
                            Store[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch
            {
                /* Ignore corruption or missing file on startup */
            }
        }

        private static void SaveToDisk()
        {
            try
            {
                var json = JsonSerializer.Serialize(Store, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(MemoryFilePath, json);
            }
            catch
            {
                /* Ignore disk write error */
            }
        }

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
                SaveToDisk();
                return Task.FromResult($"Memory stored for key '{key}'. (Saved to disk: .agent_memory.json)");
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
