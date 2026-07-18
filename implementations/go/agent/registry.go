package agent

import (
	"encoding/json"
	"fmt"
	"github.com/sashabaranov/go-openai"
)

type ToolFunc func(args map[string]interface{}) (string, error)

type ToolRegistry struct {
	tools   map[string]ToolFunc
	schemas []openai.Tool
}

func NewToolRegistry() *ToolRegistry {
	return &ToolRegistry{
		tools:   make(map[string]ToolFunc),
		schemas: make([]openai.Tool, 0),
	}
}

func (r *ToolRegistry) Register(name, description string, f ToolFunc, parametersJson json.RawMessage) {
	r.tools[name] = f

	// Unmarshal the parametersJson into a map to pass to the library
	var parameters interface{}
	_ = json.Unmarshal(parametersJson, &parameters)

	tool := openai.Tool{
		Type: openai.ToolTypeFunction,
		Function: &openai.FunctionDefinition{
			Name:        name,
			Description: description,
			Parameters:  parameters,
		},
	}
	r.schemas = append(r.schemas, tool)
}

func (r *ToolRegistry) GetToolSchemas() []openai.Tool {
	return r.schemas
}

func (r *ToolRegistry) ExecuteTool(name string, argsJson string) string {
	f, ok := r.tools[name]
	if !ok {
		return fmt.Sprintf("Error: Tool '%s' not found.", name)
	}

	var args map[string]interface{}
	if err := json.Unmarshal([]byte(argsJson), &args); err != nil {
		return fmt.Sprintf("Error parsing arguments for tool '%s': %v", name, err)
	}

	result, err := f(args)
	if err != nil {
		return fmt.Sprintf("Error executing tool '%s': %v", name, err)
	}
	return result
}
