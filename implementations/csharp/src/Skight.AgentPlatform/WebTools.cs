using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public static class WebTools
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        public static async Task<string> FetchUrlContentAsync(string argsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(argsJson);
                var root = doc.RootElement;
                if (!root.TryGetProperty("url", out var urlProp))
                {
                    return "Error: Missing 'url' argument.";
                }

                var url = urlProp.GetString()!;
                var response = await HttpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return $"Error fetching URL ({response.StatusCode}): {response.ReasonPhrase}";
                }

                var html = await response.Content.ReadAsStringAsync();
                var text = StripHtml(html);
                return ToolSecurity.TruncateOutput(50_000, text);
            }
            catch (Exception ex)
            {
                return $"Error executing web_fetch_content: {ex.Message}";
            }
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;
            // Remove script and style elements
            var clean = Regex.Replace(html, @"<(script|style)[^>]*?>.*?</\1>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            // Remove HTML tags
            clean = Regex.Replace(clean, @"<[^>]+>", " ");
            // Normalize whitespace
            return Regex.Replace(clean, @"\s+", " ").Trim();
        }
    }
}
