using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public delegate Task<string> ClarificationCallback(string question, List<string> options, bool isMultiSelect);

    public static class ClarifyTool
    {
        public static readonly string SchemaJson = @"{
            ""type"": ""object"",
            ""properties"": {
                ""question"": { ""type"": ""string"", ""description"": ""The question or design decision requiring user clarification."" },
                ""options"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""List of selectable options for the user."" },
                ""is_multi_select"": { ""type"": ""boolean"", ""description"": ""Whether multiple options can be selected."" }
            },
            ""required"": [""question"", ""options""]
        }";

        public static Func<string, Task<string>> CreateHandler(ClarificationCallback? callback = null)
        {
            return async (argsJson) =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(argsJson);
                    var root = doc.RootElement;
                    string question = root.GetProperty("question").GetString() ?? "Clarification requested";
                    var options = root.GetProperty("options")
                        .EnumerateArray()
                        .Select(e => e.GetString() ?? string.Empty)
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();

                    bool isMultiSelect = root.TryGetProperty("is_multi_select", out var elem) && elem.GetBoolean();

                    if (callback != null)
                    {
                        string userResponse = await callback(question, options, isMultiSelect);
                        return $"User selected: {userResponse}";
                    }
                    else
                    {
                        string defaultChoice = options.Count > 0 ? options[0] : "No option provided";
                        Console.WriteLine($"  [Clarify Tool] (Non-interactive mode) Defaulting to option: {defaultChoice}");
                        return $"User selected (default): {defaultChoice}";
                    }
                }
                catch (Exception ex)
                {
                    return $"Error executing clarify_tool: {ex.Message}";
                }
            };
        }

        public static void Register(ToolRegistry registry, ClarificationCallback? callback = null)
        {
            registry.Register("clarify_tool", "Ask the user one or more multiple-choice questions to resolve underspecified requirements or solicit design choices.", CreateHandler(callback), SchemaJson);
        }
    }
}
