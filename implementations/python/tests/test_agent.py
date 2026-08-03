import unittest
import sys
import os

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from agent import Agent, TurnResult
from registry import registry


class TestAgentWorkflow(unittest.TestCase):

    def test_turn_result_dataclass_initialization(self):
        result = TurnResult(
            final_response="Hello",
            messages=[],
            api_calls=1,
            completed=True,
            exit_reason="text_response",
        )
        self.assertEqual(result.final_response, "Hello")
        self.assertEqual(result.api_calls, 1)
        self.assertTrue(result.completed)
        self.assertFalse(result.failed)
        self.assertFalse(result.interrupted)
        self.assertEqual(result.exit_reason, "text_response")

    def test_tool_registry_execute_tool_success(self):
        registry.register(
            name="test_echo",
            description="Echo tool",
            func=lambda msg="": f"Echo: {msg}",
            schema={"type": "object", "properties": {"msg": {"type": "string"}}},
        )
        result = registry.execute_tool("test_echo", {"msg": "hello"})
        self.assertEqual(result, "Echo: hello")

    def test_tool_registry_unregistered_tool(self):
        result = registry.execute_tool("non_existent_tool", {})
        self.assertIn("Error: Tool 'non_existent_tool' not found.", result)

    def test_agent_interrupt_guard(self):
        agent = Agent(api_key="dummy_key")
        agent.request_interrupt()
        result = agent.run("Hello")
        self.assertTrue(result.interrupted)
        self.assertEqual(result.exit_reason, "interrupted")

    def test_context_window_protection(self):
        agent = Agent(api_key="dummy_key", context_window_limit=10)
        messages = [{"role": "system", "content": "sys"}] + [
            {"role": "user", "content": f"msg {i}"} for i in range(15)
        ]
        compressed = agent._compress_context_if_needed(messages)
        self.assertLessEqual(len(compressed), 10)
        self.assertEqual(compressed[0]["content"], "sys")


if __name__ == "__main__":
    unittest.main()
