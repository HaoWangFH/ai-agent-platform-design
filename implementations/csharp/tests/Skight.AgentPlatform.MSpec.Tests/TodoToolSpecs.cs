using System;
using System.Text.Json;
using Machine.Specifications;
using FluentAssertions;
using Skight.AgentPlatform;

namespace Skight.AgentPlatform.MSpec.Tests
{
    [Subject("TODO Tool Module - Session Task Checklist Management")]
    public class When_agent_manages_todo_checklist
    {
        Establish context = () =>
        {
            _addJson = JsonSerializer.Serialize(new { task = "Run integration tests" });
            _completeJson = JsonSerializer.Serialize(new { id = 1 });
        };

        Because of = () =>
        {
            _addResult = TodoTool.AddTodoAsync(_addJson).GetAwaiter().GetResult();
            _listResultBefore = TodoTool.ListTodosAsync().GetAwaiter().GetResult();
            _completeResult = TodoTool.CompleteTodoAsync(_completeJson).GetAwaiter().GetResult();
            _listResultAfter = TodoTool.ListTodosAsync().GetAwaiter().GetResult();
        };

        It should_add_task_to_todo_list = () =>
            _addResult.Should().Contain("Run integration tests");

        It should_show_uncompleted_task_in_list = () =>
            _listResultBefore.Should().Contain("[ ] Run integration tests");

        It should_mark_task_as_completed = () =>
            _listResultAfter.Should().Contain("[x] Run integration tests");

        static string _addJson;
        static string _completeJson;
        static string _addResult;
        static string _listResultBefore;
        static string _completeResult;
        static string _listResultAfter;
    }
}
