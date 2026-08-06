using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Generic;
using System.Text.Json;

namespace Skight.AgentPlatform
{
    class Program
    {
        static async Task<string?> GetEntraIdTokenAsync(string tenantId, string clientId, string clientSecret, string audienceId)
        {
            using var client = new HttpClient();
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("scope", $"{audienceId}/.default")
            });
            
            var response = await client.PostAsync($"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token", content);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to fetch token: {response.StatusCode}");
                return null;
            }
            
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("access_token").GetString();
        }

        static async Task Main(string[] args)
        {
            // Load config from local .env file
            var envPath = ".env";
            if (!File.Exists(envPath))
            {
                var dir = Directory.GetCurrentDirectory();
                while (dir != null)
                {
                    var path = Path.Combine(dir, ".env");
                    if (File.Exists(path))
                    {
                        envPath = path;
                        break;
                    }
                    dir = Directory.GetParent(dir)?.FullName;
                }
            }

            if (File.Exists(envPath))
            {
                Console.WriteLine($"Loaded environment from {envPath}");
                foreach (var line in File.ReadAllLines(envPath))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var val = parts[1].Trim().Trim('"');
                        Environment.SetEnvironmentVariable(key, val);
                    }
                }
            }

            var apiKey = Environment.GetEnvironmentVariable("AZURE_API_KEY") ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var gatewayEndpoint = Environment.GetEnvironmentVariable("GATEWAY_ENDPOINT");
            var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
            var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");
            var audienceId = Environment.GetEnvironmentVariable("GATEWAY_AUDIENCE_ID");
            
            string? endpoint = null;
            string model = "gpt-4o";
            string? jwtToken = null;
            
            if (!string.IsNullOrEmpty(gatewayEndpoint))
            {
                var uri = new Uri(gatewayEndpoint);
                endpoint = $"{uri.Scheme}://{uri.Host}/";
                if (gatewayEndpoint.Contains("/deployments/"))
                {
                    var parts = gatewayEndpoint.Split("/deployments/");
                    if (parts.Length > 1)
                    {
                        model = parts[1].Split('/')[0];
                    }
                }
            }

            if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret) && !string.IsNullOrEmpty(audienceId))
            {
                Console.WriteLine("Fetching Entra ID token...");
                jwtToken = await GetEntraIdTokenAsync(tenantId, clientId, clientSecret, audienceId);
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Warning: API Key not set.");
                apiKey = "dummy_key_to_allow_initialization";
            }

            Console.WriteLine($"Initializing C# Agent (Model: {model})...");
            
            var registry = new ToolRegistry();
            var specPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "spec"));
            if (!Directory.Exists(specPath))
            {
                specPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "spec"));
            }
            
            try
            {
                Tools.RegisterMockTools(registry, specPath);
            } 
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load mock tools from {specPath}: {ex.Message}");
            }

            var config = new AgentConfig
            {
                ApiKey = apiKey,
                Model = model,
                Endpoint = endpoint,
                JwtToken = jwtToken
            };

            var agent = new AgentRunner(config, registry);
            
            Console.WriteLine("Agent is ready. Type 'exit' or 'quit' to stop.");

            while (true)
            {
                Console.Write("> ");
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input)) continue;
                if (input.Trim().ToLower() is "exit" or "quit") break;

                try
                {
                    await agent.RunAsync(input);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }
    }
}
