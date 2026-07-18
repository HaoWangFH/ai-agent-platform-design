using System;
using System.IO;
using System.Threading.Tasks;

namespace AgentPlatform
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Warning: OPENAI_API_KEY environment variable not set.");
                Console.WriteLine("You can still run this, but the LLM calls will fail if no key is provided.");
                // For Azure.AI.OpenAI without a key, it might throw immediately, 
                // but we'll pass a dummy key just to construct the client.
                apiKey = "dummy_key_to_allow_initialization";
            }

            Console.WriteLine("Initializing C# Agent...");
            
            var registry = new ToolRegistry();
            var specPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "spec"));
            if (!Directory.Exists(specPath))
            {
                // Fallback for running in different directories (e.g. from IDE or raw source)
                specPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "spec"));
            }
            
            try
            {
                Tools.RegisterMockTools(registry, specPath);
            } 
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load mock tools from {specPath}: {ex.Message}");
            }

            var agent = new Agent(apiKey, registry);
            
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
