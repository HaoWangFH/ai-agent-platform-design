using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public static class MediaTools
    {
        public static Task<string> InspectImageAsync(string workspaceRoot, string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var root = doc.RootElement;
                if (!root.TryGetProperty("path", out var pathProp))
                {
                    return Task.FromResult("Error: Missing 'path' argument.");
                }

                var relPath = pathProp.GetString()!;
                var fullPath = Path.Combine(workspaceRoot, relPath);

                if (!File.Exists(fullPath))
                {
                    return Task.FromResult($"Error: Image file '{relPath}' does not exist.");
                }

                var fileInfo = new FileInfo(fullPath);
                var bytes = File.ReadAllBytes(fullPath);
                var base64 = Convert.ToBase64String(bytes);
                var ext = fileInfo.Extension.TrimStart('.').ToLower();
                var mimeType = ext switch
                {
                    "png" => "image/png",
                    "jpg" or "jpeg" => "image/jpeg",
                    "gif" => "image/gif",
                    "webp" => "image/webp",
                    "svg" => "image/svg+xml",
                    _ => "application/octet-stream"
                };

                var result = new
                {
                    path = relPath,
                    sizeBytes = fileInfo.Length,
                    mimeType = mimeType,
                    base64Preview = base64.Length > 100 ? base64.Substring(0, 100) + "..." : base64,
                    dataUri = $"data:{mimeType};base64,{base64}"
                };

                return Task.FromResult(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Error inspecting image: {ex.Message}");
            }
        }

        public static Task<string> InspectAudioAsync(string workspaceRoot, string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var root = doc.RootElement;
                if (!root.TryGetProperty("path", out var pathProp))
                {
                    return Task.FromResult("Error: Missing 'path' argument.");
                }

                var relPath = pathProp.GetString()!;
                var fullPath = Path.Combine(workspaceRoot, relPath);

                if (!File.Exists(fullPath))
                {
                    return Task.FromResult($"Error: Audio file '{relPath}' does not exist.");
                }

                var fileInfo = new FileInfo(fullPath);
                var bytes = File.ReadAllBytes(fullPath);
                var base64 = Convert.ToBase64String(bytes);
                var ext = fileInfo.Extension.TrimStart('.').ToLower();
                var mimeType = ext switch
                {
                    "mp3" => "audio/mp3",
                    "wav" => "audio/wav",
                    "m4a" => "audio/m4a",
                    "ogg" => "audio/ogg",
                    "flac" => "audio/flac",
                    "aac" => "audio/aac",
                    _ => "application/octet-stream"
                };

                var result = new
                {
                    path = relPath,
                    sizeBytes = fileInfo.Length,
                    mimeType = mimeType,
                    base64Preview = base64.Length > 100 ? base64.Substring(0, 100) + "..." : base64,
                    dataUri = $"data:{mimeType};base64,{base64}"
                };

                return Task.FromResult(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Error inspecting audio: {ex.Message}");
            }
        }
    }
}
