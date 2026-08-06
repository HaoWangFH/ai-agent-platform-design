using System;
using System.Text.Json;
using Machine.Specifications;
using FluentAssertions;
using Skight.AgentPlatform;

namespace Skight.AgentPlatform.MSpec.Tests
{
    [Subject("Web Tools Module - URL Content Fetching")]
    public class When_agent_fetches_content_from_web_url
    {
        Establish context = () =>
        {
            _urlJson = JsonSerializer.Serialize(new { url = "https://httpbin.org/html" });
        };

        Because of = () =>
        {
            _content = WebTools.FetchUrlContentAsync(_urlJson).GetAwaiter().GetResult();
        };

        It should_return_clean_text_content_without_html_tags = () =>
            _content.Should().NotContain("<html")
                .And.NotBeNullOrWhiteSpace();

        static string _urlJson;
        static string _content;
    }
}
