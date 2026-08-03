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
    }
}
