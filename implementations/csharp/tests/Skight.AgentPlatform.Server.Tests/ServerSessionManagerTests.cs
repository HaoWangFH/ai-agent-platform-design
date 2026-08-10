using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Skight.AgentPlatform;
using Skight.AgentPlatform.Server.Controllers;
using Skight.AgentPlatform.Server.Services;
using Xunit;

namespace Skight.AgentPlatform.Server.Tests
{
    public class ServerSessionManagerTests
    {
        [Fact]
        public void GetOrCreateSession_IsolatesUsersAndSessions()
        {
            var memoryStore = SqliteMemoryStore.CreateInMemory();
            var config = new AgentConfig { Model = "gpt-4o", ApiKey = "test_key" };
            var manager = new AgentSessionManager(memoryStore, config);

            var runnerUser1SessionA = manager.GetOrCreateSession("user1", "sessionA");
            var runnerUser1SessionB = manager.GetOrCreateSession("user1", "sessionB");
            var runnerUser2SessionA = manager.GetOrCreateSession("user2", "sessionA");

            Assert.NotSame(runnerUser1SessionA, runnerUser1SessionB);
            Assert.NotSame(runnerUser1SessionA, runnerUser2SessionA);
            Assert.Same(runnerUser1SessionA, manager.GetOrCreateSession("user1", "sessionA"));
        }

        [Fact]
        public async Task AgentApiController_MemoryEndpoints_StoresAndQueriesMultiTenantMemories()
        {
            var memoryStore = SqliteMemoryStore.CreateInMemory();
            var config = new AgentConfig { Model = "gpt-4o", ApiKey = "test_key" };
            var manager = new AgentSessionManager(memoryStore, config);
            var controller = new AgentApiController(manager);

            var saveResult = await controller.SaveMemory(new AgentApiController.MemorySaveApiRequest("user1", "favorite_color", "blue"));
            Assert.IsType<OkObjectResult>(saveResult);

            await controller.SaveMemory(new AgentApiController.MemorySaveApiRequest("user2", "favorite_color", "red"));

            var queryResult1 = await controller.QueryMemory("user1", "color", 5) as OkObjectResult;
            Assert.NotNull(queryResult1);

            var queryResult2 = await controller.QueryMemory("user2", "color", 5) as OkObjectResult;
            Assert.NotNull(queryResult2);
        }
    }
}
