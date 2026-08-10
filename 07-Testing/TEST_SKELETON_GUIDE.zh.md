# 验收测试与 Skeleton 生成指南 (Phase 6)

在 Specification-Driven Development (Phase 4) 中，我们通过 BDD 语法定义了预期的系统行为 (如 `AGENT_LOOP_BDD_SPECS.zh.md`)。
在 Implementation (Phase 5) 和 Verification (Phase 6) 之间，我们需要将这些规范转化为**测试骨架 (Test Skeletons)**。

## 如何使用 AI 生成测试骨架

你可以使用 GitHub Copilot Chat 或 ChatGPT，利用以下流程自动生成 C# 或 F# 的测试代码：

1. **输入上下文**:
   向 AI 提供 `08-Specification-Driven-Development/AGENT_LOOP_BDD_SPECS.zh.md` 中的某个特定场景。
2. **要求输出**:
   要求 AI 使用你选择的测试框架 (C# 的 xUnit 或 F# 的 Expecto) 生成代码骨架。测试中应该包含 Arrange, Act, Assert 的空结构。

### C# 骨架示例 (xUnit)
```csharp
public class AgentLoopTests
{
    [Fact]
    public void Given_TokenLimitReached_When_ApiCalled_Then_TriggerCompression()
    {
        // Arrange: 设置达到 token 限制的对话历史
        
        // Act: 触发下一次调用
        
        // Assert: 验证最旧的消息被移除，保留了 System Prompt
        throw new NotImplementedException("骨架已生成，等待实现");
    }
}
```

### F# 骨架示例 (Expecto)
```fsharp
let agentLoopTests =
    testList "Agent Loop Tests" [
        testCase "Given TokenLimitReached When ApiCalled Then TriggerCompression" <| fun _ ->
            // Arrange
            
            // Act
            
            // Assert
            failtest "骨架已生成，等待实现"
    ]
```

遵循此指南，确保所有的 BDD 规范都有对应的自动化测试进行覆盖。
