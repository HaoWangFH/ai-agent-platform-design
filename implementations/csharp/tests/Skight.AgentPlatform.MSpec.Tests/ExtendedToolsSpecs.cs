using System;
using System.IO;
using System.Text.Json;
using Machine.Specifications;
using FluentAssertions;
using Skight.AgentPlatform;

namespace Skight.AgentPlatform.MSpec.Tests
{
    [Subject("Media Tools Module - Image Metadata and Base64 Inspection")]
    public class When_agent_inspects_image_file
    {
        Establish context = () =>
        {
            _workspace = Directory.GetCurrentDirectory();
            _imgPath = "test_sample.png";
            var fullPath = Path.Combine(_workspace, _imgPath);
            File.WriteAllBytes(fullPath, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }); // Dummy PNG header
            _jsonArg = JsonSerializer.Serialize(new { path = _imgPath });
        };

        Because of = () =>
        {
            _result = MediaTools.InspectImageAsync(_workspace, _jsonArg).GetAwaiter().GetResult();
        };

        It should_return_mime_type_and_base64_uri = () =>
            _result.Should().Contain("image/png")
                .And.Contain("data:image/png;base64");

        Cleanup after = () =>
        {
            var fullPath = Path.Combine(_workspace, _imgPath);
            if (File.Exists(fullPath)) File.Delete(fullPath);
        };

        static string _workspace;
        static string _imgPath;
        static string _jsonArg;
        static string _result;
    }

    [Subject("Automation Tools Module - Background Timer Task Scheduling")]
    public class When_agent_schedules_automation_timer
    {
        Establish context = () =>
        {
            _jsonArg = JsonSerializer.Serialize(new { seconds = 1, prompt = "Run health check" });
        };

        Because of = () =>
        {
            _result = AutomationTools.ScheduleTimerAsync(_jsonArg).GetAwaiter().GetResult();
        };

        It should_confirm_scheduling_with_task_id = () =>
            _result.Should().Contain("Timer task #")
                .And.Contain("Run health check");

        static string _jsonArg;
        static string _result;
    }
}
