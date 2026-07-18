import json
import os
from registry import registry

# Load the mock tool schemas from spec
SPEC_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "spec"))
with open(os.path.join(SPEC_DIR, "mock_tools.json"), "r") as f:
    mock_tools_spec = json.load(f)

# Find specific schema by name
def get_schema(name: str):
    for tool in mock_tools_spec:
        if tool["name"] == name:
            return tool["parameters"]
    return {}

# 1. Weather Tool
def get_weather(location: str, unit: str = "celsius") -> str:
    """Mock implementation of get_weather."""
    # In a real app, this would call a weather API.
    if "san francisco" in location.lower():
        return f"The weather in {location} is 16 degrees {unit.capitalize()} and foggy."
    return f"The weather in {location} is 22 degrees {unit.capitalize()} and sunny."

registry.register(
    name="get_weather",
    description="Get the current weather in a given location.",
    func=get_weather,
    schema=get_schema("get_weather")
)

# 2. Read File Tool
def read_file(path: str) -> str:
    """Mock implementation of read_file."""
    try:
        with open(path, "r", encoding="utf-8") as f:
            return f.read()
    except Exception as e:
        return f"Error reading file: {str(e)}"

registry.register(
    name="read_file",
    description="Read the contents of a file.",
    func=read_file,
    schema=get_schema("read_file")
)
