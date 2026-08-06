using System;
using System.Text.Json;
using Machine.Specifications;
using FluentAssertions;
using Skight.AgentPlatform;

namespace Skight.AgentPlatform.MSpec.Tests
{
    [Subject("Memory Tool Module - Persistent Context Store and Recall")]
    public class When_agent_stores_and_recalls_key_value_memory
    {
        Establish context = () =>
        {
            _storeJson = JsonSerializer.Serialize(new { key = "user_preference", value = "Dark Mode" });
            _recallJson = JsonSerializer.Serialize(new { key = "user_preference" });
        };

        Because of = () =>
        {
            _storeResult = MemoryTool.StoreMemoryAsync(_storeJson).GetAwaiter().GetResult();
            _recallResult = MemoryTool.RecallMemoryAsync(_recallJson).GetAwaiter().GetResult();
        };

        It should_confirm_storage_of_memory = () =>
            _storeResult.Should().Contain("user_preference");

        It should_successfully_recall_stored_memory = () =>
            _recallResult.Should().Contain("Dark Mode");

        static string _storeJson;
        static string _recallJson;
        static string _storeResult;
        static string _recallResult;
    }
}
