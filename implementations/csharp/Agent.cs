using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.AI.OpenAI;

namespace AgentPlatform
{
    public class Agent
    {
        private readonly OpenAIClient _client;
        private readonly string _model;
        private readonly List<ChatRequestMessage> _messages = new();
        private readonly ToolRegistry _registry;

        public Agent(string apiKey, ToolRegistry registry, string model = "gpt-4-turbo")
        {
            _client = new OpenAIClient(apiKey, new OpenAIClientOptions());
            _model = model;
            _registry = registry;
            InitializeSystemPrompt();
        }

        private void InitializeSystemPrompt()
        {
            var systemPrompt = "You are a helpful AI assistant. You have access to various tools. " +
                               "When asked to perform a task, use the tools to gather information and take actions before answering.";
            _messages.Add(new ChatRequestSystemMessage(systemPrompt));
        }

        public async Task<string> RunAsync(string userInput)
        {
            Console.WriteLine($"\nUser: {userInput}");
            _messages.Add(new ChatRequestUserMessage(userInput));

            while (true)
            {
                var options = new ChatCompletionsOptions(_model)
                {
                    Temperature = 0.7f
                };

                foreach (var msg in _messages)
                {
                    options.Messages.Add(msg);
                }

                var tools = _registry.GetToolSchemas();
                foreach (var tool in tools)
                {
                    options.Tools.Add(new ChatCompletionsFunctionToolDefinition(tool));
                }

                var response = await _client.GetChatCompletionsAsync(options);
                var choice = response.Value.Choices[0];
                var message = choice.Message;

                if (message.ToolCalls != null && message.ToolCalls.Count > 0)
                {
                    var assistantMessage = new ChatRequestAssistantMessage(message.Content);
                    
                    foreach (var toolCall in message.ToolCalls)
                    {
                        if (toolCall is ChatCompletionsFunctionToolCall functionCall)
                        {
                            assistantMessage.ToolCalls.Add(functionCall);
                            var name = functionCall.Name;
                            var args = functionCall.Arguments;
                            
                            Console.WriteLine($"  [Tool Execution] {name}({args})");
                            
                            var result = await _registry.ExecuteToolAsync(name, args);
                            Console.WriteLine($"  [Tool Result] {result}");
                            
                            _messages.Add(new ChatRequestToolMessage(result, functionCall.Id));
                        }
                    }
                    // Insert the assistant message before the tool results
                    _messages.Insert(_messages.Count - message.ToolCalls.Count, assistantMessage);
                    
                    continue; // Loop again to send tool results to LLM
                }

                var finalText = message.Content ?? string.Empty;
                Console.WriteLine($"Assistant: {finalText}");
                _messages.Add(new ChatRequestAssistantMessage(finalText));
                return finalText;
            }
        }
    }
}
