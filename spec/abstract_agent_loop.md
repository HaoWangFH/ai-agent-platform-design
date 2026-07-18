# Abstract Agent Specification

## 1. Core State
Every agent implementation must manage the following state:
- `MessageHistory`: An ordered list of messages (User, Assistant, Tool).
- `ToolRegistry`: A collection of available tools.
- `SystemPrompt`: A static instruction set given to the LLM.

## 2. The Agent Loop
The core execution loop should follow this sequence:
1. Append the user's input to `MessageHistory`.
2. Send `MessageHistory` + `SystemPrompt` + `ToolRegistry` schemas to the LLM API.
3. Wait for the LLM response.
4. If the response contains **text only**:
   - Append text to `MessageHistory` as an Assistant message.
   - Yield text to the user.
   - **Break** the loop.
5. If the response contains **tool calls**:
   - Append the tool call request to `MessageHistory` as an Assistant message.
   - For each tool call:
     - Look up the tool in the `ToolRegistry`.
     - Execute the tool (serially or in parallel, depending on implementation).
     - Append the result to `MessageHistory` as a Tool message.
   - **Continue** the loop (goto step 2).

## 3. Tool Interface
Tools must support a standard schema format (e.g., JSON Schema) so the LLM knows how to call them. Tools should return a string (or be serialized to a string) representing the outcome of their execution.
