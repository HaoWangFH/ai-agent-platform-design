using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public static class IntegrationTools
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        public static async Task<string> SendWebhookAsync(string argsJson)
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
                var method = root.TryGetProperty("method", out var mProp) ? mProp.GetString()!.ToUpper() : "POST";
                var payload = root.TryGetProperty("payload", out var pProp) ? pProp.ToString() : "{}";

                using var request = new HttpRequestMessage(new HttpMethod(method), url);
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                if (root.TryGetProperty("headers", out var headersProp) && headersProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var header in headersProp.EnumerateObject())
                    {
                        request.Headers.TryAddWithoutValidation(header.Name, header.Value.ToString());
                    }
                }

                var response = await HttpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();
                var truncatedBody = ToolSecurity.TruncateOutput(5000, body);

                return $"Webhook HTTP {(int)response.StatusCode} ({response.ReasonPhrase}):\n{truncatedBody}";
            }
            catch (Exception ex)
            {
                return $"Error sending webhook: {ex.Message}";
            }
        }
    }
}
