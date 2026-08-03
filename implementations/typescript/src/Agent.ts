import OpenAI from 'openai';
import { registry } from './ToolRegistry';

export interface TurnResult {
  finalResponse: string;
  messages: OpenAI.Chat.Completions.ChatCompletionMessageParam[];
  apiCalls: number;
  completed: boolean;
  failed: boolean;
  interrupted: boolean;
  exitReason: string;
  error?: string;
}

export interface AgentConfig {
  apiKey?: string;
  model?: string;
  maxIterations?: number;
  maxRetries?: number;
  contextWindowLimit?: number;
}

export class Agent {
  private client: OpenAI;
  private model: string;
  private maxIterations: number;
  private maxRetries: number;
  private contextWindowLimit: number;
  private messages: OpenAI.Chat.Completions.ChatCompletionMessageParam[] = [];
  private interruptRequested: boolean = false;

  constructor(apiKey?: string, model: string = 'gpt-4-turbo', config: AgentConfig = {}) {
    this.client = new OpenAI({ apiKey: config.apiKey || apiKey });
    this.model = config.model || model;
    this.maxIterations = config.maxIterations ?? 10;
    this.maxRetries = config.maxRetries ?? 3;
    this.contextWindowLimit = config.contextWindowLimit ?? 30;
    this.initializeSystemPrompt();
  }

  private initializeSystemPrompt() {
    const systemPrompt = `You are a helpful AI assistant. You have access to various tools. 
When asked to perform a task, use the tools to gather information and take actions before answering.`;
    this.messages.push({ role: 'system', content: systemPrompt });
  }

  public requestInterrupt(): void {
    this.interruptRequested = true;
  }

  public prepareApiMessages(
    msgs: OpenAI.Chat.Completions.ChatCompletionMessageParam[]
  ): OpenAI.Chat.Completions.ChatCompletionMessageParam[] {
    // Phase 2.2: Shallow copy for API payload to keep canonical history clean
    return msgs.map((m) => ({ ...m }));
  }

  public compressContextIfNeeded(
    msgs: OpenAI.Chat.Completions.ChatCompletionMessageParam[]
  ): OpenAI.Chat.Completions.ChatCompletionMessageParam[] {
    // Phase 2.3: Context window protection
    if (msgs.length <= this.contextWindowLimit) {
      return msgs;
    }

    console.log(
      `  [Context Window Protection] History size (${msgs.length}) > limit (${this.contextWindowLimit}). Trimming middle history...`
    );

    const systemPrompt = msgs[0];
    const recentCount = this.contextWindowLimit - 3;
    const recentMessages = msgs.slice(-recentCount);

    while (recentMessages.length > 0 && recentMessages[0].role === 'tool') {
      recentMessages.shift();
    }

    const summaryMsg: OpenAI.Chat.Completions.ChatCompletionMessageParam = {
      role: 'system',
      content: `[System: Previous conversation history was trimmed to fit context window. ${
        msgs.length - recentMessages.length - 1
      } earlier messages summarized.]`,
    };

    return [systemPrompt, summaryMsg, ...recentMessages];
  }

  async run(userInput: string): Promise<TurnResult> {
    // --- Phase 1: Turn Prologue ---
    console.log(`\nUser: ${userInput}`);
    this.messages.push({ role: 'user', content: userInput });

    let apiCalls = 0;
    let emptyContentRetries = 0;

    // --- Phase 2: Main Conversation Loop ---
    while (apiCalls < this.maxIterations) {
      // 2.1 Pre-API Checks
      if (this.interruptRequested) {
        this.interruptRequested = false;
        console.log('  [Turn Exit] Turn interrupted by user.');
        return {
          finalResponse: '',
          messages: this.messages,
          apiCalls,
          completed: false,
          failed: false,
          interrupted: true,
          exitReason: 'interrupted',
        };
      }

      apiCalls++;

      // 2.2 & 2.3 Message Preparation and Context Compression
      let preparedMessages = this.prepareApiMessages(this.messages);
      preparedMessages = this.compressContextIfNeeded(preparedMessages);

      // 2.4 Inner Retry Loop for LLM API Call
      let response: OpenAI.Chat.Completions.ChatCompletion | null = null;
      let lastError: any = null;

      for (let retry = 0; retry < this.maxRetries; retry++) {
        try {
          const tools = registry.getToolSchemas();
          response = await this.client.chat.completions.create({
            model: this.model,
            messages: preparedMessages,
            tools: tools.length > 0 ? tools : undefined,
          });
          break;
        } catch (err: any) {
          lastError = err;
          console.log(`  [API Error Retry ${retry + 1}/${this.maxRetries}] ${err.message || err}`);
          if (retry === this.maxRetries - 1) {
            return {
              finalResponse: '',
              messages: this.messages,
              apiCalls,
              completed: false,
              failed: true,
              interrupted: false,
              exitReason: 'api_error',
              error: String(err.message || err),
            };
          }
          await new Promise((resolve) => setTimeout(resolve, Math.pow(2, retry) * 1000));
        }
      }

      if (!response) {
        return {
          finalResponse: '',
          messages: this.messages,
          apiCalls,
          completed: false,
          failed: true,
          interrupted: false,
          exitReason: 'no_response',
          error: String(lastError),
        };
      }

      const message = response.choices[0].message;
      this.messages.push(message);

      // 2.6 Tool Call Execution Path
      if (message.tool_calls && message.tool_calls.length > 0) {
        for (const toolCall of message.tool_calls) {
          const name = toolCall.function.name;
          const callId = toolCall.id;

          // Self-correction for unknown tools
          const registeredTools = registry.getToolSchemas().map((t) => t.function.name);
          if (!registeredTools.includes(name)) {
            const errorMsg = `Error: Tool '${name}' is not registered. Available tools: ${registeredTools.join(', ')}`;
            console.log(`  [Tool Validation Error] ${errorMsg}`);
            this.messages.push({
              role: 'tool',
              tool_call_id: callId,
              content: errorMsg,
            });
            continue;
          }

          // Parse JSON arguments
          let args: Record<string, any> = {};
          try {
            args = toolCall.function.arguments ? JSON.parse(toolCall.function.arguments) : {};
          } catch (jsonErr: any) {
            const errorMsg = `Error: Invalid JSON arguments for tool '${name}': ${jsonErr.message}`;
            console.log(`  [JSON Parse Error] ${errorMsg}`);
            this.messages.push({
              role: 'tool',
              tool_call_id: callId,
              content: errorMsg,
            });
            continue;
          }

          console.log(`  [Tool Execution] ${name}(${JSON.stringify(args)})`);

          // Execute tool with runtime exception handling
          try {
            const result = await registry.executeTool(name, args);
            console.log(`  [Tool Result] ${result}`);
            this.messages.push({
              role: 'tool',
              tool_call_id: callId,
              content: String(result),
            });
          } catch (execErr: any) {
            const result = `Error executing tool '${name}': ${execErr.message || execErr}`;
            console.log(`  [Tool Runtime Error] ${result}`);
            this.messages.push({
              role: 'tool',
              tool_call_id: callId,
              content: result,
            });
          }
        }
        // Continue loop to send tool results to LLM
        continue;
      }

      // 2.7 Final Text Response Path
      let finalText = (message.content || '').trim();

      // Empty Response Recovery
      if (!finalText) {
        if (emptyContentRetries < 2) {
          emptyContentRetries++;
          console.log('  [Empty Response Recovery] Retrying with prompt nudge...');
          this.messages.push({
            role: 'user',
            content: 'Please provide a complete text response summarizing your answer.',
          });
          continue;
        } else {
          finalText = '(empty response)';
        }
      }

      console.log(`Assistant: ${finalText}`);

      // --- Phase 4: Turn Finalization ---
      return {
        finalResponse: finalText,
        messages: this.messages,
        apiCalls,
        completed: true,
        failed: false,
        interrupted: false,
        exitReason: 'text_response',
      };
    }

    // Exceeded iteration budget
    console.log(`  [Turn Exit] Reached max iterations (${this.maxIterations}).`);
    return {
      finalResponse: 'Reached maximum iteration limit.',
      messages: this.messages,
      apiCalls,
      completed: false,
      failed: true,
      interrupted: false,
      exitReason: 'budget_exhausted',
    };
  }
}
