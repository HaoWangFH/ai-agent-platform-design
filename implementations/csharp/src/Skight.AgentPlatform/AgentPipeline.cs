using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.AI.OpenAI;

namespace Skight.AgentPlatform
{
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

    public class AgentPipeline
    {
        private readonly OpenAIClient _client;
        private readonly string _model;
        private readonly ToolRegistry _registry;

        public AgentPipeline(OpenAIClient client, string model, ToolRegistry registry)
        {
            _client = client;
            _model = model;
            _registry = registry;
        }

        private List<ChatRequestMessage> PrepareApiMessages(List<ChatRequestMessage> msgs)
        {
            return new List<ChatRequestMessage>(msgs);
        }

        private List<ChatRequestMessage> CompressContextIfNeeded(List<ChatRequestMessage> msgs, int contextWindowLimit)
        {
            return ContextCompressor.Compress(0.80, contextWindowLimit, msgs);
        }

        public async Task<TurnResult> RunTurnLoopAsync(AgentSessionState session, AgentConfig config, Func<List<FunctionDefinition>, List<ChatRequestMessage>, Task<ChatCompletions>>? customLlmCaller = null)
        {
            int apiCalls = 0;
            int emptyContentRetries = 0;

            while (apiCalls < config.MaxIterations)
            {
                if (session.InterruptRequested)
                {
                    session.InterruptRequested = false;
                    Console.WriteLine("  [Turn Exit] Turn interrupted by user.");
                    return new TurnResult { FinalResponse = string.Empty, Messages = session.Messages, ApiCalls = apiCalls, Interrupted = true, ExitReason = "interrupted" };
                }

                apiCalls++;

                DrainSteering(session);

                var preparedMessages = CompressContextIfNeeded(PrepareApiMessages(session.Messages), config.ContextWindowLimit);

                ChatCompletions? completions = null;
                Exception? lastError = null;

                for (int retry = 0; retry < config.MaxRetries; retry++)
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
                            var options = new ChatCompletionsOptions(_model, preparedMessages) { Temperature = 0.7f };

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
                        Console.WriteLine($"  [API Error Retry {retry + 1}/{config.MaxRetries}] {ex.Message}");
                        if (retry == config.MaxRetries - 1)
                        {
                            return new TurnResult { FinalResponse = string.Empty, Messages = session.Messages, ApiCalls = apiCalls, Failed = true, ExitReason = "api_error", Error = ex.Message };
                        }
                        await Task.Delay((int)Math.Pow(2, retry) * 1000);
                    }
                }

                if (completions == null || completions.Choices.Count == 0)
                {
                    return new TurnResult { FinalResponse = string.Empty, Messages = session.Messages, ApiCalls = apiCalls, Failed = true, ExitReason = "no_response", Error = lastError?.Message ?? "No choices returned." };
                }

                var choice = completions.Choices[0];
                var message = choice.Message;

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

                            if (!registeredNames.Contains(name))
                            {
                                var errorMsg = $"Error: Tool '{name}' is not registered. Available tools: {string.Join(", ", registeredNames)}";
                                Console.WriteLine($"  [Tool Validation Error] {errorMsg}");
                                session.Messages.Add(new ChatRequestToolMessage(errorMsg, callId));
                                continue;
                            }

                            string cleanArgs = SanitizeJsonArguments(name, functionCall.Arguments);

                            try { using var doc = JsonDocument.Parse(cleanArgs); }
                            catch (Exception jsonEx)
                            {
                                var errorMsg = $"Error: Invalid JSON arguments for tool '{name}': {jsonEx.Message}";
                                Console.WriteLine($"  [JSON Parse Error] {errorMsg}");
                                session.Messages.Add(new ChatRequestToolMessage(errorMsg, callId));
                                continue;
                            }

                            Console.WriteLine($"  [Tool Execution] {name}({cleanArgs})");

                            try
                            {
                                var mutationTools = new HashSet<string> { "file_write", "write_to_file", "file_patch", "replace_file_content" };
                                var verificationTools = new HashSet<string> { "read_terminal", "terminal_execute", "run_tests", "dotnet_test" };

                                if (mutationTools.Contains(name))
                                {
                                    session.HasFileMutations = true;
                                    session.HasExecutedVerification = false;
                                }
                                else if (verificationTools.Contains(name))
                                {
                                    session.HasExecutedVerification = true;
                                }

                                var swTool = System.Diagnostics.Stopwatch.StartNew();
                                var result = await _registry.ExecuteToolAsync(name, cleanArgs);
                                swTool.Stop();
                                AgentTelemetry.TrackToolExecution(session.SessionId, session.UserId, session.TurnCount, name, swTool.ElapsedMilliseconds, cleanArgs, result);
                                Console.WriteLine($"  [Tool Result] {result}");
                                session.Messages.Add(new ChatRequestToolMessage(result, callId));
                            }
                            catch (Exception execEx)
                            {
                                var errorResult = $"Error executing tool '{name}': {execEx.Message}";
                                Console.WriteLine($"  [Tool Runtime Error] {errorResult}");
                                session.Messages.Add(new ChatRequestToolMessage(errorResult, callId));
                            }
                        }
                    }
                    session.Messages.Insert(session.Messages.Count - message.ToolCalls.Count, assistantMessage);
                    continue;
                }

                var finalText = (message.Content ?? string.Empty).Trim();

                if (string.IsNullOrEmpty(finalText))
                {
                    if (emptyContentRetries < 2)
                    {
                        emptyContentRetries++;
                        Console.WriteLine("  [Empty Response Recovery] Retrying with prompt nudge...");
                        session.Messages.Add(new ChatRequestUserMessage("Please provide a complete text response summarizing your answer."));
                        continue;
                    }
                    else
                    {
                        finalText = "(empty response)";
                    }
                }

                if (session.HasFileMutations && !session.HasExecutedVerification && session.PreVerifyNudges < 2)
                {
                    session.PreVerifyNudges++;
                    Console.WriteLine("  [Pre-Verify Quality Gate] Intercepted completed turn. Files modified without verification. Prompting agent to verify...");
                    session.Messages.Add(new ChatRequestAssistantMessage(finalText));
                    session.Messages.Add(new ChatRequestUserMessage("You modified files during this turn. Please verify your changes by executing unit tests or build commands before concluding."));
                    continue;
                }

                session.Messages.Add(new ChatRequestAssistantMessage(finalText));
                AgentTelemetry.TrackTurnEnd(session.SessionId, session.UserId, session.TurnCount, 0L, finalText, "completed");
                return new TurnResult { FinalResponse = finalText, Messages = session.Messages, ApiCalls = apiCalls, Completed = true, ExitReason = "text_response" };
            }

            Console.WriteLine($"  [Turn Exit] Reached max iterations ({config.MaxIterations}).");
            return new TurnResult { FinalResponse = "Reached maximum iteration limit.", Messages = session.Messages, ApiCalls = apiCalls, Failed = true, ExitReason = "budget_exhausted" };
        }

        private void DrainSteering(AgentSessionState session)
        {
            if (session.SteeringQueue.IsEmpty) return;

            var items = new List<string>();
            while (session.SteeringQueue.TryDequeue(out var item))
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    items.Add(item);
                }
            }

            if (items.Count == 0) return;

            string steeringContent = "\n\n[USER STEERING INTERRUPT]: " + string.Join("\n", items);
            Console.WriteLine($"  [Pre-API Steering Drain] Injected {items.Count} mid-turn steering message(s)");

            if (session.Messages.Count > 0)
            {
                var lastMsg = session.Messages[session.Messages.Count - 1];
                if (lastMsg is ChatRequestToolMessage toolMsg)
                {
                    session.Messages[session.Messages.Count - 1] = new ChatRequestToolMessage(toolMsg.Content + steeringContent, toolMsg.ToolCallId);
                }
                else if (lastMsg is ChatRequestUserMessage userMsg)
                {
                    session.Messages[session.Messages.Count - 1] = new ChatRequestUserMessage(userMsg.Content + steeringContent);
                }
                else
                {
                    session.Messages.Add(new ChatRequestUserMessage("[USER STEERING INTERRUPT]: " + string.Join("\n", items)));
                }
            }
            else
            {
                session.Messages.Add(new ChatRequestUserMessage("[USER STEERING INTERRUPT]: " + string.Join("\n", items)));
            }
        }

        public static string SanitizeJsonArguments(string toolName, string rawArgs)
        {
            if (string.IsNullOrWhiteSpace(rawArgs)) return "{}";
            string clean = rawArgs.Trim();

            // 1. Extract JSON object if concatenated with leading tokens (e.g. "types.fs" {"path":"types.fs"})
            int braceIndex = clean.IndexOf('{');
            if (braceIndex > 0)
            {
                int lastBrace = clean.LastIndexOf('}');
                if (lastBrace > braceIndex)
                {
                    clean = clean.Substring(braceIndex, lastBrace - braceIndex + 1);
                }
            }

            // 2. Unescape double-encoded JSON strings
            if (clean.StartsWith("\"") && clean.EndsWith("\""))
            {
                try
                {
                    var unescaped = JsonSerializer.Deserialize<string>(clean);
                    if (!string.IsNullOrWhiteSpace(unescaped)) clean = unescaped.Trim();
                }
                catch { }
            }

            // 3. Re-check brace extraction after unescaping
            braceIndex = clean.IndexOf('{');
            if (braceIndex > 0)
            {
                int lastBrace = clean.LastIndexOf('}');
                if (lastBrace > braceIndex)
                {
                    clean = clean.Substring(braceIndex, lastBrace - braceIndex + 1);
                }
            }

            // 4. If clean is a raw unquoted string or file path without braces (e.g. "types.fs")
            if (!clean.StartsWith("{"))
            {
                var rawString = clean.Trim('"');
                clean = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["path"] = rawString,
                    ["command"] = rawString,
                    ["key"] = rawString,
                    ["task"] = rawString,
                    ["text"] = rawString,
                    ["url"] = rawString
                });
            }

            return clean;
        }
    }
}
