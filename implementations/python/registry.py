import inspect
import json
from typing import Any, Callable, Dict

from pydantic import TypeAdapter

class ToolRegistry:
    def __init__(self):
        self._tools: Dict[str, Callable] = {}
        self._schemas: Dict[str, dict] = {}

    def register(self, name: str, description: str, func: Callable, schema: dict):
        """Register a tool with its JSON schema."""
        self._tools[name] = func
        self._schemas[name] = {
            "type": "function",
            "function": {
                "name": name,
                "description": description,
                "parameters": schema
            }
        }

    def get_tool_schemas(self) -> list[dict]:
        """Return the list of tool schemas for the LLM."""
        return list(self._schemas.values())

    def execute_tool(self, name: str, kwargs: dict) -> str:
        """Execute a tool by name and return its result as a string."""
        if name not in self._tools:
            return f"Error: Tool '{name}' not found."
        
        try:
            func = self._tools[name]
            result = func(**kwargs)
            
            # Ensure the result is always a string representation
            if not isinstance(result, str):
                result = json.dumps(result, default=str)
                
            return result
        except Exception as e:
            return f"Error executing tool '{name}': {str(e)}"

# A global registry instance for convenience
registry = ToolRegistry()
