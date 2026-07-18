import * as readline from 'readline';
import { Agent } from './Agent';

// Import tools to ensure they are registered
import './tools';

async function main() {
  const apiKey = process.env.OPENAI_API_KEY;
  if (!apiKey) {
    console.warn("Warning: OPENAI_API_KEY environment variable not set.");
    console.warn("You can still run this, but the LLM calls will fail if no key is provided.");
  }

  console.log("Initializing TypeScript Agent...");
  const agent = new Agent(apiKey);
  
  console.log("Agent is ready. Type 'exit' or 'quit' to stop.");
  
  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
    prompt: '> '
  });

  rl.prompt();

  rl.on('line', async (line) => {
    const input = line.trim();
    if (input.toLowerCase() === 'exit' || input.toLowerCase() === 'quit') {
      rl.close();
      return;
    }
    
    if (input) {
      try {
        await agent.run(input);
      } catch (e: any) {
        console.error(`Error: ${e.message}`);
      }
    }
    rl.prompt();
  }).on('close', () => {
    process.exit(0);
  });
}

main().catch(console.error);
