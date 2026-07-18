using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace AgentPlatform
{
    public static class Tools
    {
        public static void RegisterMockTools(ToolRegistry registry, string specDirPath)
        {
            var mockToolsJsonPath = Path.Combine(specDirPath, "mock_tools.json");
            var jsonString = File.ReadAllText(mockToolsJsonPath);
            using var document = JsonDocument.Parse(jsonString);
            
            var toolsArray = document.RootElement.EnumerateArray().ToList();

            // 1. Weather Tool
            var weatherToolSpec = toolsArray.FirstOrDefault(t => t.GetProperty("name").GetString() == "get_weather");
            if (weatherToolSpec.ValueKind != JsonValueKind.Undefined)
            {
                registry.Register(
                    name: "get_weather",
                    description: weatherToolSpec.GetProperty("description").GetString() ?? "",
                    func: async (argsJson) => 
                    {
                        using var argsDoc = JsonDocument.Parse(argsJson);
                        var location = argsDoc.RootElement.TryGetProperty("location", out var locProp) ? locProp.GetString() : "";
                        var unit = argsDoc.RootElement.TryGetProperty("unit", out var unitProp) ? unitProp.GetString() : "celsius";
                        
                        if ((location ?? "").ToLower().Contains("san francisco"))
                        {
                            return $"The weather in {location} is 16 degrees {unit} and foggy.";
                        }
                        return $"The weather in {location} is 22 degrees {unit} and sunny.";
                    },
                    parametersJson: weatherToolSpec.GetProperty("parameters").GetRawText()
                );
            }

            // 2. Read File Tool
            var readFileToolSpec = toolsArray.FirstOrDefault(t => t.GetProperty("name").GetString() == "read_file");
            if (readFileToolSpec.ValueKind != JsonValueKind.Undefined)
            {
                registry.Register(
                    name: "read_file",
                    description: readFileToolSpec.GetProperty("description").GetString() ?? "",
                    func: async (argsJson) => 
                    {
                        try
                        {
                            using var argsDoc = JsonDocument.Parse(argsJson);
                            var filePath = argsDoc.RootElement.TryGetProperty("path", out var pathProp) ? pathProp.GetString() : "";
                            
                            if (string.IsNullOrEmpty(filePath)) return "Error: path is required";
                            
                            return await File.ReadAllTextAsync(filePath);
                        }
                        catch (Exception ex)
                        {
                            return $"Error reading file: {ex.Message}";
                        }
                    },
                    parametersJson: readFileToolSpec.GetProperty("parameters").GetRawText()
                );
            }
        }
    }
}
