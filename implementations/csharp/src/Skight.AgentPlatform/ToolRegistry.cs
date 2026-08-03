using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.AI.OpenAI;

namespace Skight.AgentPlatform
{
    public class ToolRegistry
    {
        private readonly Dictionary<string, Func<string, Task<string>>> _tools = new();
        private readonly List<FunctionDefinition> _schemas = new();

        public void Register(string name, string description, Func<string, Task<string>> func, string parametersJson)
        {
            _tools[name] = func;
            
            using var doc = JsonDocument.Parse(parametersJson);
            var functionDef = new FunctionDefinition
            {
                Name = name,
                Description = description,
                Parameters = BinaryData.FromString(parametersJson)
            };
            _schemas.Add(functionDef);
        }

        public async Task<string> ExecuteToolAsync(string name, string argumentsJson)
        {
            if (!_tools.TryGetValue(name, out var func))
            {
                return $"Error: Tool '{name}' not found.";
            }

            try
            {
                return await func(argumentsJson);
            }
            catch (Exception ex)
            {
                return $"Error executing tool '{name}': {ex.Message}";
            }
        }

        public List<FunctionDefinition> GetToolSchemas()
        {
            return _schemas;
        }
    }
}
