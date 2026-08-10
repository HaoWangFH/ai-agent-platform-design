using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Skight.AgentPlatform;

namespace Skight.AgentPlatform.Server.Services
{
    public class AgentSessionManager
    {
        private readonly ConcurrentDictionary<string, AgentRunner> _sessions = new();
        private readonly IMemoryStore _memoryStore;
        private readonly AgentConfig _defaultConfig;

        public AgentSessionManager(IMemoryStore memoryStore, AgentConfig defaultConfig)
        {
            _memoryStore = memoryStore;
            _defaultConfig = defaultConfig;
        }

        public AgentRunner GetOrCreateSession(string userId, string sessionId)
        {
            string sessionKey = $"{userId}:{sessionId}";

            return _sessions.GetOrAdd(sessionKey, _ =>
            {
                var registry = new ToolRegistry();
                Tools.RegisterCoreTools(registry, Environment.CurrentDirectory);
                ClarifyTool.Register(registry, null);

                return new AgentRunner(_defaultConfig, registry);
            });
        }

        public IMemoryStore GetMemoryStore() => _memoryStore;
    }
}
