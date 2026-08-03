import json
import os
import time
from dataclasses import dataclass, field
from typing import List, Dict, Any, Optional

from openai import OpenAI
from registry import registry

SPEC_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "spec"))


@dataclass
class TurnResult:
    """Structured result returned after completing an agent turn."""
    final_response: str
    messages: List[Dict[str, Any]]
    api_calls: int = 0
    completed: bool = False
    failed: bool = False
    interrupted: bool = False
    exit_reason: str = "unknown"
    error: Optional[str] = None


class Agent:
    def __init__(
        self,
        api_key: Optional[str] = None,
        model: str = "gpt-4-turbo",
        max_iterations: int = 10,
        max_retries: int = 3,
        context_window_limit: int = 30,
    ):
        self.client = OpenAI(api_key=api_key)
        self.model = model
        self.max_iterations = max_iterations
        self.max_retries = max_retries
        self.context_window_limit = context_window_limit
        self.messages: List[Dict[str, Any]] = []
        self._interrupt_requested: bool = False
        
        self._initialize_system_prompt()

    def _initialize_system_prompt(self):
        system_prompt = (
            "You are a helpful AI assistant. You have access to various tools. "
            "When asked to perform a task, use the tools to gather information "
            "and take actions before answering."
        )
        self.messages.append({"role": "system", "content": system_prompt})

    def request_interrupt(self):
        """Signal the agent turn to interrupt on the next iteration check."""
        self._interrupt_requested = True

    def _prepare_api_messages(self, messages: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """Phase 2.2: Prepare API-only shallow copy of messages with role/sequence validation."""
        api_messages = []
        for msg in messages:
            api_msg = msg.copy()
            # Strip internal metadata fields if present
            api_msg.pop("_internal_id", None)
            api_messages.append(api_msg)
        return api_messages

    def _compress_context_if_needed(self, messages: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """Phase 2.3: Context window protection.
        
        Trims middle history if messages exceed context_window_limit while preserving:
        - Index 0: System Prompt
        - Index 1: Initial User Prompt (if present)
        - Last N: Recent conversation history
        """
        if len(messages) <= self.context_window_limit:
            return messages

        print(f"  [Context Window Protection] History size ({len(messages)}) > limit ({self.context_window_limit}). Trimming middle history...")
        
        system_prompt = messages[0]
        recent_count = self.context_window_limit - 3
        recent_messages = messages[-recent_count:]

        # Ensure recent_messages starts with a valid role (not an orphaned tool result)
        while recent_messages and recent_messages[0].get("role") == "tool":
            recent_messages.pop(0)

        summary_msg = {
            "role": "system",
            "content": f"[System: Previous conversation history was trimmed to fit context window. {len(messages) - len(recent_messages) - 1} earlier messages summarized.]",
        }
        
        return [system_prompt, summary_msg] + recent_messages

    def run(self, user_input: str) -> TurnResult:
        """Phase 1 -> Phase 2 -> Phase 3 -> Phase 4 Conversation Loop execution."""
        # --- Phase 1: Turn Prologue ---
        print(f"\nUser: {user_input}")
        self.messages.append({"role": "user", "content": user_input})
        
        api_call_count = 0
        self._interrupt_requested = False
        empty_content_retries = 0

        # --- Phase 2: Main Conversation Loop ---
        while api_call_count < self.max_iterations:
            # 2.1 Pre-API Checks
            if self._interrupt_requested:
                print("  [Turn Exit] Turn interrupted by user.")
                return TurnResult(
                    final_response="",
                    messages=self.messages,
                    api_calls=api_call_count,
                    interrupted=True,
                    exit_reason="interrupted",
                )

            api_call_count += 1

            # 2.2 & 2.3 Message Preparation and Context Compression
            prepared_messages = self._prepare_api_messages(self.messages)
            prepared_messages = self._compress_context_if_needed(prepared_messages)

            # 2.4 Inner Retry Loop for LLM API Call
            response = None
            for retry in range(self.max_retries):
                try:
                    tool_schemas = registry.get_tool_schemas()
                    response = self.client.chat.completions.create(
                        model=self.model,
                        messages=prepared_messages,
                        tools=tool_schemas if tool_schemas else None,
                    )
                    break
                except Exception as e:
                    print(f"  [API Error Retry {retry + 1}/{self.max_retries}] {e}")
                    if retry == self.max_retries - 1:
                        return TurnResult(
                            final_response="",
                            messages=self.messages,
                            api_calls=api_call_count,
                            failed=True,
                            exit_reason="api_error",
                            error=str(e),
                        )
                    time.sleep(2 ** retry)

            message = response.choices[0].message
            message_dict = message.model_dump(exclude_unset=True)
            
            # Append assistant message to canonical history
            self.messages.append(message_dict)

            # 2.6 Tool Call Execution Path
            if message.tool_calls:
                for tool_call in message.tool_calls:
                    name = tool_call.function.name
                    call_id = tool_call.id
                    
                    # Validate tool exists in registry (Self-correction path)
                    available_tools = list(registry._tools.keys())
                    if name not in registry._tools:
                        error_msg = f"Error: Tool '{name}' is not registered. Available tools: {available_tools}"
                        print(f"  [Tool Validation Error] {error_msg}")
                        self.messages.append({
                            "role": "tool",
                            "tool_call_id": call_id,
                            "name": name,
                            "content": error_msg,
                        })
                        continue

                    # Validate JSON arguments
                    try:
                        args = json.loads(tool_call.function.arguments) if tool_call.function.arguments else {}
                    except json.JSONDecodeError as json_err:
                        error_msg = f"Error: Invalid JSON arguments for tool '{name}': {json_err}"
                        print(f"  [JSON Parse Error] {error_msg}")
                        self.messages.append({
                            "role": "tool",
                            "tool_call_id": call_id,
                            "name": name,
                            "content": error_msg,
                        })
                        continue

                    print(f"  [Tool Execution] {name}({args})")
                    
                    # Execute tool with exception handling
                    try:
                        result = registry.execute_tool(name, args)
                        print(f"  [Tool Result] {result}")
                    except Exception as exec_err:
                        result = f"Error executing tool '{name}': {exec_err}"
                        print(f"  [Tool Runtime Error] {result}")
                    
                    self.messages.append({
                        "role": "tool",
                        "tool_call_id": call_id,
                        "name": name,
                        "content": str(result),
                    })
                
                # Continue loop to send tool results back to LLM
                continue

            # 2.7 Final Text Response Path
            final_text = (message.content or "").strip()
            
            # Empty Response Recovery
            if not final_text:
                if empty_content_retries < 2:
                    empty_content_retries += 1
                    print("  [Empty Response Recovery] Retrying with prompt nudge...")
                    self.messages.append({
                        "role": "user",
                        "content": "Please provide a complete text response summarizing your answer.",
                    })
                    continue
                else:
                    final_text = "(empty response)"

            print(f"Assistant: {final_text}")
            
            # --- Phase 4: Turn Finalization ---
            return TurnResult(
                final_response=final_text,
                messages=self.messages,
                api_calls=api_call_count,
                completed=True,
                exit_reason="text_response",
            )

        # Exceeded iteration budget
        print(f"  [Turn Exit] Reached max iterations ({self.max_iterations}).")
        return TurnResult(
            final_response="Reached maximum iteration limit.",
            messages=self.messages,
            api_calls=api_call_count,
            completed=False,
            failed=True,
            exit_reason="budget_exhausted",
        )
