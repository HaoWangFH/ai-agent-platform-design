using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

    public class TurnResult
    {
        public string FinalResponse { get; set; } = string.Empty;
        public List<ChatRequestMessage> Messages { get; set; } = new();
        public int ApiCalls { get; set; }
        public bool Completed { get; set; }
        public bool Failed { get; set; }
        public bool Interrupted { get; set; }
        public string ExitReason { get; set; } = "unknown";
        public string? Error { get; set; }
    }

    public class Agent
    {
        private readonly OpenAIClient _client;
        private readonly string _model;
        private readonly List<ChatRequestMessage> _messages = new();
        private readonly ToolRegistry _registry;
        private bool _interruptRequested = false;

        public int MaxIterations { get; set; } = 10;
        public int MaxRetries { get; set; } = 3;
        public int ContextWindowLimit { get; set; } = 30;

        public Agent(string apiKey, ToolRegistry registry, string model = "gpt-4o", string? endpoint = null, string? jwtToken = null)
        {
            var options = new OpenAIClientOptions();
            if (!string.IsNullOrEmpty(jwtToken))
            {
                options.AddPolicy(new BearerTokenPolicy(jwtToken), Azure.Core.HttpPipelinePosition.PerCall);
            }

            if (!string.IsNullOrEmpty(endpoint))
            {
                // Azure OpenAI initialization
                _client = new OpenAIClient(new Uri(endpoint), new Azure.AzureKeyCredential(apiKey), options);
            }
            else
            {
                // Standard OpenAI initialization
                _client = new OpenAIClient(apiKey, options);
            }
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

        public void RequestInterrupt()
        {
            _interruptRequested = true;
        }

        private List<ChatRequestMessage> PrepareApiMessages(List<ChatRequestMessage> msgs)
        {
            // Phase 2.2: Shallow copy for API payload
            return new List<ChatRequestMessage>(msgs);
        }

        private List<ChatRequestMessage> CompressContextIfNeeded(List<ChatRequestMessage> msgs)
        {
            // Phase 2.3: Context window protection
            if (msgs.Count <= ContextWindowLimit)
            {
                return msgs;
            }

            Console.WriteLine($"  [Context Window Protection] History size ({msgs.Count}) > limit ({ContextWindowLimit}). Trimming middle history...");

            var systemPrompt = msgs[0];
            int recentCount = ContextWindowLimit - 3;
            var recentMessages = msgs.Skip(msgs.Count - recentCount).ToList();

            while (recentMessages.Count > 0 && recentMessages[0] is ChatRequestToolMessage)
            {
                recentMessages.RemoveAt(0);
            }

            var summaryMsg = new ChatRequestSystemMessage(
                $"[System: Previous conversation history was trimmed to fit context window. {msgs.Count - recentMessages.Count - 1} earlier messages summarized.]"
            );

            var result = new List<ChatRequestMessage> { systemPrompt, summaryMsg };
            result.AddRange(recentMessages);
            return result;
        }

        public async Task<TurnResult> RunAsync(string userInput, Func<List<FunctionDefinition>, List<ChatRequestMessage>, Task<ChatCompletions>>? customLlmCaller = null)
        {
            // --- Phase 1: Turn Prologue ---
            Console.WriteLine($"\nUser: {userInput}");
            _messages.Add(new ChatRequestUserMessage(userInput));

            int apiCalls = 0;
            int emptyContentRetries = 0;

            // --- Phase 2: Main Conversation Loop ---
            while (apiCalls < MaxIterations)
            {
                // 2.1 Pre-API Checks
                if (_interruptRequested)
                {
                    _interruptRequested = false; // Reset after handling
                    Console.WriteLine("  [Turn Exit] Turn interrupted by user.");
                    return new TurnResult
                    {
                        FinalResponse = string.Empty,
                        Messages = _messages,
                        ApiCalls = apiCalls,
                        Interrupted = true,
                        ExitReason = "interrupted"
                    };
                }

                apiCalls++;

                // 2.2 & 2.3 Message Preparation and Context Compression
                var preparedMessages = PrepareApiMessages(_messages);
                preparedMessages = CompressContextIfNeeded(preparedMessages);

                // 2.4 Inner Retry Loop for LLM API Call
                ChatCompletions? completions = null;
                Exception? lastError = null;

                for (int retry = 0; retry < MaxRetries; retry++)
                {
                    try
                    {
                        var toolDefinitions = _registry.GetToolSchemas();
                        if (customLlmCaller != null)
                        {
                            completions = await customLlmCaller(toolDefinitions.ToList(), preparedMessages);
                        }
                        else
                        {
                            var options = new ChatCompletionsOptions(_model, preparedMessages)
                            {
                                Temperature = 0.7f
                            };

                            foreach (var toolDef in toolDefinitions)
                            {
                                options.Tools.Add(new ChatCompletionsFunctionToolDefinition(toolDef));
                            }

                            var response = await _client.GetChatCompletionsAsync(options);
                            completions = response.Value;
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        Console.WriteLine($"  [API Error Retry {retry + 1}/{MaxRetries}] {ex.Message}");
                        if (retry == MaxRetries - 1)
                        {
                            return new TurnResult
                            {
                                FinalResponse = string.Empty,
                                Messages = _messages,
                                ApiCalls = apiCalls,
                                Failed = true,
                                ExitReason = "api_error",
                                Error = ex.Message
                            };
                        }
                        await Task.Delay((int)Math.Pow(2, retry) * 1000);
                    }
                }

                if (completions == null || completions.Choices.Count == 0)
                {
                    return new TurnResult
                    {
                        FinalResponse = string.Empty,
                        Messages = _messages,
                        ApiCalls = apiCalls,
                        Failed = true,
                        ExitReason = "no_response",
                        Error = lastError?.Message ?? "No choices returned."
                    };
                }

                var choice = completions.Choices[0];
                var message = choice.Message;

                // 2.6 Tool Call Execution Path
                if (message.ToolCalls != null && message.ToolCalls.Count > 0)
                {
                    var assistantMessage = new ChatRequestAssistantMessage(message.Content);
                    var registeredSchemas = _registry.GetToolSchemas();
                    var registeredNames = new HashSet<string>(registeredSchemas.Select(s => s.Name));

                    foreach (var toolCall in message.ToolCalls)
                    {
                        if (toolCall is ChatCompletionsFunctionToolCall functionCall)
                        {
                            assistantMessage.ToolCalls.Add(functionCall);
                            var name = functionCall.Name;
                            var callId = functionCall.Id;

                            // Self-correction for unregistered tool
                            if (!registeredNames.Contains(name))
                            {
                                var errorMsg = $"Error: Tool '{name}' is not registered. Available tools: {string.Join(", ", registeredNames)}";
                                Console.WriteLine($"  [Tool Validation Error] {errorMsg}");
                                _messages.Add(new ChatRequestToolMessage(errorMsg, callId));
                                continue;
                            }

                            // Validate JSON arguments
                            try
                            {
                                using var doc = JsonDocument.Parse(string.IsNullOrEmpty(functionCall.Arguments) ? "{}" : functionCall.Arguments);
                            }
                            catch (Exception jsonEx)
                            {
                                var errorMsg = $"Error: Invalid JSON arguments for tool '{name}': {jsonEx.Message}";
                                Console.WriteLine($"  [JSON Parse Error] {errorMsg}");
                                _messages.Add(new ChatRequestToolMessage(errorMsg, callId));
                                continue;
                            }

                            Console.WriteLine($"  [Tool Execution] {name}({functionCall.Arguments})");

                            // Execute tool with runtime exception handling
                            try
                            {
                                var result = await _registry.ExecuteToolAsync(name, functionCall.Arguments);
                                Console.WriteLine($"  [Tool Result] {result}");
                                _messages.Add(new ChatRequestToolMessage(result, callId));
                            }
                            catch (Exception execEx)
                            {
                                var errorResult = $"Error executing tool '{name}': {execEx.Message}";
                                Console.WriteLine($"  [Tool Runtime Error] {errorResult}");
                                _messages.Add(new ChatRequestToolMessage(errorResult, callId));
                            }
                        }
                    }

                    // Insert the assistant message before the tool result messages
                    _messages.Insert(_messages.Count - message.ToolCalls.Count, assistantMessage);
                    continue; // Loop again to send tool results to LLM
                }

                // 2.7 Final Text Response Path
                var finalText = (message.Content ?? string.Empty).Trim();

                // Empty Response Recovery
                if (string.IsNullOrEmpty(finalText))
                {
                    if (emptyContentRetries < 2)
                    {
                        emptyContentRetries++;
                        Console.WriteLine("  [Empty Response Recovery] Retrying with prompt nudge...");
                        _messages.Add(new ChatRequestUserMessage("Please provide a complete text response summarizing your answer."));
                        continue;
                    }
                    else
                    {
                        finalText = "(empty response)";
                    }
                }

                Console.WriteLine($"Assistant: {finalText}");
                _messages.Add(new ChatRequestAssistantMessage(finalText));

                // --- Phase 4: Turn Finalization ---
                return new TurnResult
                {
                    FinalResponse = finalText,
                    Messages = _messages,
                    ApiCalls = apiCalls,
                    Completed = true,
                    ExitReason = "text_response"
                };
            }

            // Exceeded iteration budget
            Console.WriteLine($"  [Turn Exit] Reached max iterations ({MaxIterations}).");
            return new TurnResult
            {
                FinalResponse = "Reached maximum iteration limit.",
                Messages = _messages,
                ApiCalls = apiCalls,
                Failed = true,
                ExitReason = "budget_exhausted"
            };
        }
    }
}
