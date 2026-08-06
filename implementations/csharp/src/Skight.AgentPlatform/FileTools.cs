using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public static class FileTools
    {
        private const int DefaultMaxOutputBytes = 100 * 1024;
        private static readonly Regex DiffBlockPattern = new(@"(?s)<<<<<<< SEARCH\s*(.*?)\s*=======\s*(.*?)\s*>>>>>>> REPLACE", RegexOptions.Compiled);

        public static async Task<string> ReadFileAsync(string workspaceRoot, string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var root = doc.RootElement;
                if (!root.TryGetProperty("path", out var pathProp) || pathProp.ValueKind != JsonValueKind.String)
                    return "Error: Missing required string property 'path'.";

                var path = pathProp.GetString();
                var safePath = ToolSecurity.ValidatePathInSandbox(workspaceRoot, path!);

                if (!File.Exists(safePath)) return $"Error: File '{path}' not found.";
                
                var text = await File.ReadAllTextAsync(safePath);
                int maxBytes = root.TryGetProperty("max_bytes", out var maxProp) && maxProp.TryGetInt32(out int mb) ? mb : DefaultMaxOutputBytes;
                
                return ToolSecurity.TruncateOutput(maxBytes, text);
            }
            catch (Exception ex)
            {
                return $"Error reading file: {ex.Message}";
            }
        }

        public static async Task<string> WriteFileAsync(string workspaceRoot, string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var root = doc.RootElement;
                if (!root.TryGetProperty("path", out var pathProp)) return "Error: Missing required string property 'path'.";
                if (!root.TryGetProperty("content", out var contentProp)) return "Error: Missing required string property 'content'.";

                var path = pathProp.GetString();
                var content = contentProp.GetString();
                var safePath = ToolSecurity.ValidatePathInSandbox(workspaceRoot, path!);

                var dir = Path.GetDirectoryName(safePath);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

                await File.WriteAllTextAsync(safePath, content);
                return $"Wrote {content?.Length ?? 0} characters to '{path}'.";
            }
            catch (Exception ex)
            {
                return $"Error writing file: {ex.Message}";
            }
        }

        public static async Task<string> EditFileAsync(string workspaceRoot, string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var root = doc.RootElement;
                if (!root.TryGetProperty("path", out var pathProp)) return "Error: Missing property 'path'.";
                
                var path = pathProp.GetString();
                var safePath = ToolSecurity.ValidatePathInSandbox(workspaceRoot, path!);
                if (!File.Exists(safePath)) return $"Error: File '{path}' not found.";

                var original = await File.ReadAllTextAsync(safePath);
                string updated = original;
                int replacements = 0;

                if (root.TryGetProperty("search", out var searchProp) && root.TryGetProperty("replace", out var replaceProp))
                {
                    var search = searchProp.GetString();
                    var replace = replaceProp.GetString();
                    if (!string.IsNullOrEmpty(search))
                    {
                        int index = original.IndexOf(search, StringComparison.Ordinal);
                        if (index < 0) return "Error: Search text not found in file.";
                        updated = original.Remove(index, search.Length).Insert(index, replace);
                        replacements = 1;
                    }
                }
                else if (root.TryGetProperty("patch", out var patchProp))
                {
                    var patch = patchProp.GetString() ?? string.Empty;
                    var matches = DiffBlockPattern.Matches(patch);
                    if (matches.Count == 0) return "Error: Patch format invalid. Expected one or more SEARCH/REPLACE blocks.";

                    foreach (Match m in matches)
                    {
                        var search = m.Groups[1].Value;
                        var replace = m.Groups[2].Value;
                        int index = updated.IndexOf(search, StringComparison.Ordinal);
                        if (index < 0) return $"Error: Patch apply failed. SEARCH block not found: {(search.Length > 120 ? search.Substring(0, 120) + "..." : search)}";
                        updated = updated.Remove(index, search.Length).Insert(index, replace);
                        replacements++;
                    }
                }
                else
                {
                    return "Error: Edit requires either search+replace or patch arguments.";
                }

                await File.WriteAllTextAsync(safePath, updated);
                return $"Applied {replacements} edit(s) to '{path}'.";
            }
            catch (Exception ex)
            {
                return $"Error editing file: {ex.Message}";
            }
        }
    }
}
