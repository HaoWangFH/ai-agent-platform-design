import * as fs from 'fs';
import * as path from 'path';
import { registry } from './ToolRegistry';

// Load mock tool schemas from spec
const specPath = path.resolve(__dirname, '../../../spec/mock_tools.json');
const mockToolsSpec = JSON.parse(fs.readFileSync(specPath, 'utf8'));

function getSchema(name: string): any {
  const tool = mockToolsSpec.find((t: any) => t.name === name);
  return tool ? tool.parameters : {};
}

// 1. Weather Tool
registry.register(
  'get_weather',
  'Get the current weather in a given location.',
  async (args: { location: string; unit?: string }) => {
    const location = args.location || '';
    const unit = args.unit || 'celsius';
    
    if (location.toLowerCase().includes('san francisco')) {
      return `The weather in ${location} is 16 degrees ${unit} and foggy.`;
    }
    return `The weather in ${location} is 22 degrees ${unit} and sunny.`;
  },
  getSchema('get_weather')
);

// 2. Read File Tool
registry.register(
  'read_file',
  'Read the contents of a file.',
  async (args: { path: string }) => {
    try {
      return fs.readFileSync(args.path, 'utf8');
    } catch (e: any) {
      return `Error reading file: ${e.message}`;
    }
  },
  getSchema('read_file')
);
