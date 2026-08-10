using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Skight.AgentPlatform.Tests
{
    public class ClarifyToolTests
    {
        [Fact]
        public async Task ClarifyTool_InvokesCallback_AndReturnsSelection()
        {
            string askedQuestion = string.Empty;
            ClarificationCallback callback = (q, opts, multi) =>
            {
                askedQuestion = q;
                return Task.FromResult(opts[1]);
            };

            var handler = ClarifyTool.CreateHandler(callback);
            string argsJson = @"{ ""question"": ""Which database?"", ""options"": [""SQLite"", ""PostgreSQL"", ""Redis""] }";

            string result = await handler(argsJson);

            Assert.Equal("Which database?", askedQuestion);
            Assert.Contains("User selected: PostgreSQL", result);
        }

        [Fact]
        public async Task ClarifyTool_NonInteractiveMode_DefaultsToFirstOption()
        {
            var handler = ClarifyTool.CreateHandler(null);
            string argsJson = @"{ ""question"": ""Which database?"", ""options"": [""SQLite"", ""PostgreSQL""] }";

            string result = await handler(argsJson);

            Assert.Contains("User selected (default): SQLite", result);
        }
    }
}
