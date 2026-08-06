using System;
using System.Threading.Tasks;
using Azure.AI.OpenAI;

namespace Skight.AgentPlatform
{
    public class BearerTokenPolicy : Azure.Core.Pipeline.HttpPipelineSynchronousPolicy
    {
        private readonly string _token;
        public BearerTokenPolicy(string token) => _token = token;
        
        public override void OnSendingRequest(Azure.Core.HttpMessage message)
        {
            message.Request.Headers.SetValue("Authorization", $"Bearer {_token}");
        }
    }

    public class AgentRunner
    {
        private readonly AgentPipeline _pipeline;
        private readonly AgentConfig _config;
        private readonly AgentSessionState _session;

        public AgentRunner(AgentConfig config, ToolRegistry registry)
        {
            _config = config;
            _session = new AgentSessionState();
            
            var options = new OpenAIClientOptions();
            if (!string.IsNullOrEmpty(config.JwtToken))
            {
                options.AddPolicy(new BearerTokenPolicy(config.JwtToken), Azure.Core.HttpPipelinePosition.PerCall);
            }

            OpenAIClient client;
            if (!string.IsNullOrEmpty(config.Endpoint))
            {
                client = new OpenAIClient(new Uri(config.Endpoint), new Azure.AzureKeyCredential(config.ApiKey), options);
            }
            else
            {
                client = new OpenAIClient(config.ApiKey, options);
            }

            _pipeline = new AgentPipeline(client, config.Model, registry);

            InitializeSystemPrompt();
        }

        private void InitializeSystemPrompt()
        {
            var systemPrompt = "You are a helpful AI assistant. You have access to various tools. " +
                               "When asked to perform a task, use the tools to gather information and take actions before answering.";
            _session.Messages.Add(new ChatRequestSystemMessage(systemPrompt));
        }

        public void RequestInterrupt()
        {
            _session.InterruptRequested = true;
        }

        public async Task<TurnResult> RunAsync(string userInput, Func<List<FunctionDefinition>, List<ChatRequestMessage>, Task<ChatCompletions>>? customLlmCaller = null)
        {
            Console.WriteLine($"\nUser: {userInput}");
            _session.Messages.Add(new ChatRequestUserMessage(userInput));

            return await _pipeline.RunTurnLoopAsync(_session, _config, customLlmCaller);
        }
    }
}
