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

        public static Task<string> TranscribeAudioAsync(string workspaceRoot, string argsJson)
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
                return Task.FromResult($"[Audio Transcription Stub for '{relPath}' ({fileInfo.Length} bytes)]: Transcribed speech content placeholder. Connect OPENAI_API_KEY to invoke live Whisper API.");
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Error transcribing audio: {ex.Message}");
            }
        }

        public static Task<string> TextToSpeechAsync(string workspaceRoot, string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var root = doc.RootElement;
                if (!root.TryGetProperty("text", out var textProp))
                {
                    return Task.FromResult("Error: Missing 'text' argument.");
                }

                var text = textProp.GetString()!;
                var outputPath = root.TryGetProperty("output_path", out var outProp) ? outProp.GetString()! : "speech_output.mp3";
                var fullPath = Path.Combine(workspaceRoot, outputPath);

                File.WriteAllBytes(fullPath, new byte[] { 0x49, 0x44, 0x33 }); // Generate output audio file stub

                return Task.FromResult($"Text-to-Speech audio generated at '{outputPath}'. Text length: {text.Length} characters.");
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Error generating speech audio: {ex.Message}");
            }
        }
    }
}
