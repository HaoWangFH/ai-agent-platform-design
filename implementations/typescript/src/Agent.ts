import OpenAI from 'openai';
import { registry } from './ToolRegistry';

export class Agent {
  private client: OpenAI;
  private model: string;
  private messages: OpenAI.Chat.Completions.ChatCompletionMessageParam[] = [];

  constructor(apiKey?: string, model: string = 'gpt-4-turbo') {
    this.client = new OpenAI({ apiKey });
    this.model = model;
    this.initializeSystemPrompt();
  }

  private initializeSystemPrompt() {
    const systemPrompt = `You are a helpful AI assistant. You have access to various tools. 
When asked to perform a task, use the tools to gather information and take actions before answering.`;
    this.messages.push({ role: 'system', content: systemPrompt });
  }

  async run(userInput: string): Promise<string> {
    console.log(`\nUser: ${userInput}`);
    this.messages.push({ role: 'user', content: userInput });

    while (true) {
      const tools = registry.getToolSchemas();
      
      const response = await this.client.chat.completions.create({
        model: this.model,
        messages: this.messages,
        tools: tools.length > 0 ? tools : undefined,
      });

      const message = response.choices[0].message;
      this.messages.push(message);

      if (message.tool_calls && message.tool_calls.length > 0) {
        for (const toolCall of message.tool_calls) {
          const name = toolCall.function.name;
          const args = JSON.parse(toolCall.function.arguments);
          console.log(`  [Tool Execution] ${name}(${JSON.stringify(args)})`);
          
          const result = await registry.executeTool(name, args);
          console.log(`  [Tool Result] ${result}`);
          
          this.messages.push({
            role: 'tool',
            tool_call_id: toolCall.id,
            content: result,
          });
        }
        // Continue the loop to send tool results to LLM
        continue;
      }

      const finalText = message.content || '';
      console.log(`Assistant: ${finalText}`);
      return finalText;
    }
  }
}
