import json
import os
from typing import List, Dict, Any

from openai import OpenAI
from registry import registry

# Load the system prompt from the specification
SPEC_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "spec"))

class Agent:
    def __init__(self, api_key: str = None, model: str = "gpt-4-turbo"):
        # We allow the API key to be passed in, or it defaults to the environment variable
        self.client = OpenAI(api_key=api_key)
        self.model = model
        self.messages: List[Dict[str, Any]] = []
        
        # Load system prompt
        self._initialize_system_prompt()

    def _initialize_system_prompt(self):
        # We can define a generic system prompt for the agent
        system_prompt = (
            "You are a helpful AI assistant. You have access to various tools. "
            "When asked to perform a task, use the tools to gather information "
            "and take actions before answering."
        )
        self.messages.append({"role": "system", "content": system_prompt})

    def run(self, user_input: str) -> str:
        """Run the agent loop for a new user input."""
        print(f"\nUser: {user_input}")
        self.messages.append({"role": "user", "content": user_input})

        while True:
            # 1. Call the LLM
            response = self.client.chat.completions.create(
                model=self.model,
                messages=self.messages,
                tools=registry.get_tool_schemas() if registry.get_tool_schemas() else None,
            )

            message = response.choices[0].message
            
            # 2. Append the assistant's message (which may contain tool_calls)
            message_dict = message.model_dump(exclude_unset=True)
            self.messages.append(message_dict)

            # 3. Check for tool calls
            if message.tool_calls:
                for tool_call in message.tool_calls:
                    name = tool_call.function.name
                    args = json.loads(tool_call.function.arguments)
                    print(f"  [Tool Execution] {name}({args})")
                    
                    # Execute tool
                    result = registry.execute_tool(name, args)
                    print(f"  [Tool Result] {result}")
                    
                    # Append tool result to history
                    self.messages.append({
                        "role": "tool",
                        "tool_call_id": tool_call.id,
                        "name": name,
                        "content": result
                    })
                # Continue loop to send tool results back to LLM
                continue
            
            # 4. If no tool calls, it's a final text response
            final_text = message.content or ""
            print(f"Assistant: {final_text}")
            return final_text
