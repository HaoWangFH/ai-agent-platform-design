import { z } from 'zod';

export type ToolFunction = (args: any) => Promise<string> | string;

export interface ToolDefinition {
  type: "function";
  function: {
    name: string;
    description: string;
    parameters: any; // JSON schema
  };
}

export class ToolRegistry {
  private tools: Map<string, ToolFunction> = new Map();
  private schemas: Map<string, ToolDefinition> = new Map();

  register(name: string, description: string, func: ToolFunction, schema: any): void {
    this.tools.set(name, func);
    this.schemas.set(name, {
      type: "function",
      function: {
        name,
        description,
        parameters: schema,
      },
    });
  }

  getToolSchemas(): any[] {
    return Array.from(this.schemas.values());
  }

  async executeTool(name: string, args: any): Promise<string> {
    const func = this.tools.get(name);
    if (!func) {
      return `Error: Tool '${name}' not found.`;
    }

    try {
      const result = await func(args);
      if (typeof result !== 'string') {
        return JSON.stringify(result);
      }
      return result;
    } catch (e: any) {
      return `Error executing tool '${name}': ${e.message}`;
    }
  }
}

export const registry = new ToolRegistry();
