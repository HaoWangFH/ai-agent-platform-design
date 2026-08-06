using System.Collections.Generic;
using Azure.AI.OpenAI;

namespace Skight.AgentPlatform
{
    // Extracted models from Agent.cs
    public class AgentConfig
    {
        public int MaxIterations { get; set; } = 10;
        public int MaxRetries { get; set; } = 3;
        public int ContextWindowLimit { get; set; } = 30;
        public string Model { get; set; } = "gpt-4o";
        public string? Endpoint { get; set; }
        public string? JwtToken { get; set; }
        public string ApiKey { get; set; } = string.Empty;
    }

    public class AgentSessionState
    {
        public List<ChatRequestMessage> Messages { get; set; } = new();
        public bool InterruptRequested { get; set; }
    }
}
