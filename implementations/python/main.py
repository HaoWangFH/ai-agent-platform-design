import os
import sys

# Import tools so they are registered
import tools
from agent import Agent

def main():
    # Attempt to get API key from environment
    api_key = os.environ.get("OPENAI_API_KEY")
    if not api_key:
        print("Warning: OPENAI_API_KEY environment variable not set.")
        print("You can still run this, but the LLM calls will fail if no key is provided.")
        
    print("Initializing Python Agent...")
    agent = Agent(api_key=api_key)
    
    print("Agent is ready. Type 'exit' or 'quit' to stop.")
    while True:
        try:
            user_input = input("> ")
            if user_input.strip().lower() in ["exit", "quit"]:
                break
            
            if not user_input.strip():
                continue
                
            agent.run(user_input)
            
        except KeyboardInterrupt:
            break
        except Exception as e:
            print(f"Error: {e}")

if __name__ == "__main__":
    main()
