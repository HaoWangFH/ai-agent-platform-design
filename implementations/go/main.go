package main

import (
	"bufio"
	"context"
	"encoding/json"
	"fmt"
	"io/ioutil"
	"os"
	"path/filepath"
	"strings"

	"ai-agent-go/agent"
)

type Spec struct {
	Name        string          `json:"name"`
	Description string          `json:"description"`
	Parameters  json.RawMessage `json:"parameters"`
}

func main() {
	apiKey := os.Getenv("OPENAI_API_KEY")
	if apiKey == "" {
		fmt.Println("Warning: OPENAI_API_KEY environment variable not set.")
		fmt.Println("You can still run this, but the LLM calls will fail if no key is provided.")
	}

	fmt.Println("Initializing Go Agent...")

	registry := agent.NewToolRegistry()
	
	// Try to find the spec directory
	specPath := filepath.Join("..", "..", "spec", "mock_tools.json")
	if _, err := os.Stat(specPath); os.IsNotExist(err) {
		specPath = filepath.Join("..", "spec", "mock_tools.json")
	}

	data, err := ioutil.ReadFile(specPath)
	if err == nil {
		var specs []Spec
		if err := json.Unmarshal(data, &specs); err == nil {
			for _, s := range specs {
				if s.Name == "get_weather" {
					registry.Register(s.Name, s.Description, func(args map[string]interface{}) (string, error) {
						loc, _ := args["location"].(string)
						unit, ok := args["unit"].(string)
						if !ok {
							unit = "celsius"
						}
						
						if strings.Contains(strings.ToLower(loc), "san francisco") {
							return fmt.Sprintf("The weather in %s is 16 degrees %s and foggy.", loc, unit), nil
						}
						return fmt.Sprintf("The weather in %s is 22 degrees %s and sunny.", loc, unit), nil
					}, s.Parameters)
				} else if s.Name == "read_file" {
					registry.Register(s.Name, s.Description, func(args map[string]interface{}) (string, error) {
						path, _ := args["path"].(string)
						content, err := ioutil.ReadFile(path)
						if err != nil {
							return "", err
						}
						return string(content), nil
					}, s.Parameters)
				}
			}
		} else {
			fmt.Println("Warning: failed to parse mock_tools.json", err)
		}
	} else {
		fmt.Println("Warning: failed to read mock_tools.json", err)
	}

	a := agent.NewAgent(apiKey, registry, "")
	ctx := context.Background()

	fmt.Println("Agent is ready. Type 'exit' or 'quit' to stop.")
	scanner := bufio.NewScanner(os.Stdin)

	for {
		fmt.Print("> ")
		if !scanner.Scan() {
			break
		}
		
		input := strings.TrimSpace(scanner.Text())
		if input == "" {
			continue
		}
		
		if strings.ToLower(input) == "exit" || strings.ToLower(input) == "quit" {
			break
		}

		_, err := a.Run(ctx, input)
		if err != nil {
			fmt.Printf("Error: %v\n", err)
		}
	}
}
