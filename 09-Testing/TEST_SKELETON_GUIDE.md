# Acceptance Testing & Skeleton Generation Guide (Phase 6)

In Specification-Driven Development (Phase 4), we define expected system behaviors using BDD syntax (e.g., `AGENT_LOOP_BDD_SPECS.md`).
Between Implementation (Phase 5) and Verification (Phase 6), we need to translate these specifications into **Test Skeletons**.

## How to use AI to generate test skeletons

You can use GitHub Copilot Chat or ChatGPT with the following workflow to automatically generate C# or F# test code:

1. **Input Context**:
   Provide the AI with a specific scenario from `08-Specification-Driven-Development/AGENT_LOOP_BDD_SPECS.md`.
2. **Request Output**:
   Ask the AI to generate a code skeleton using your chosen testing framework (xUnit for C# or Expecto for F#). The test should contain empty structures for Arrange, Act, and Assert.

### C# Skeleton Example (xUnit)
```csharp
public class AgentLoopTests
{
    [Fact]
    public void Given_TokenLimitReached_When_ApiCalled_Then_TriggerCompression()
    {
        // Arrange: Set up a conversation history that hits the token limit
        
        // Act: Trigger the next call
        
        // Assert: Verify the oldest message is removed and System Prompt is retained
        throw new NotImplementedException("Skeleton generated, awaiting implementation");
    }
}
```

### F# Skeleton Example (Expecto)
```fsharp
let agentLoopTests =
    testList "Agent Loop Tests" [
        testCase "Given TokenLimitReached When ApiCalled Then TriggerCompression" <| fun _ ->
            // Arrange
            
            // Act
            
            // Assert
            failtest "Skeleton generated, awaiting implementation"
    ]
```

Follow this guide to ensure all BDD specifications are covered by corresponding automated tests.
