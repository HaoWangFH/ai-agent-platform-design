import assert from 'node:assert';
import { test, describe } from 'node:test';
import { Agent } from '../src/Agent';
import { ToolRegistry } from '../src/ToolRegistry';

describe('TypeScript Agent Workflow Unit Tests', () => {
  test('ToolRegistry executes registered tools', async () => {
    const registry = new ToolRegistry();
    registry.register(
      'echo',
      'Echo tool',
      async (args) => `Echo: ${args.msg}`,
      { type: 'object', properties: { msg: { type: 'string' } } }
    );

    const result = await registry.executeTool('echo', { msg: 'hello' });
    assert.strictEqual(result, 'Echo: hello');
  });

  test('ToolRegistry handles unregistered tool error', async () => {
    const registry = new ToolRegistry();
    const result = await registry.executeTool('unknown_tool', {});
    assert.ok(result.includes("Error: Tool 'unknown_tool' not found."));
  });

  test('Agent interrupt guard triggers cleanly', async () => {
    const agent = new Agent('dummy_key');
    agent.requestInterrupt();
    const result = await agent.run('Hello');

    assert.strictEqual(result.interrupted, true);
    assert.strictEqual(result.exitReason, 'interrupted');
  });

  test('Context window protection trims middle history', () => {
    const agent = new Agent('dummy_key', 'gpt-4-turbo', { contextWindowLimit: 10 });
    const messages: any[] = [
      { role: 'system', content: 'sys' },
      ...Array.from({ length: 15 }, (_, i) => ({ role: 'user', content: `msg ${i}` })),
    ];

    const compressed = agent.compressContextIfNeeded(messages);
    assert.ok(compressed.length <= 10, 'Compressed history should be within limit');
    assert.strictEqual(compressed[0].content, 'sys');
  });
});
