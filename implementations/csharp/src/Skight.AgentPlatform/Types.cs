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
        public string SessionId { get; set; } = System.Guid.NewGuid().ToString("N");
        public string UserId { get; set; } = "default_user";
        public int TurnCount { get; set; } = 1;
        public List<ChatRequestMessage> Messages { get; set; } = new();
        public bool InterruptRequested { get; set; }
        public bool HasFileMutations { get; set; }
        public bool HasExecutedVerification { get; set; }
        public int PreVerifyNudges { get; set; }
        public System.Collections.Concurrent.ConcurrentQueue<string> SteeringQueue { get; } = new();
    }
}
