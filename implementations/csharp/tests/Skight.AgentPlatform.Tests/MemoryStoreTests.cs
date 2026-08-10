using System.Threading.Tasks;
using Xunit;

namespace Skight.AgentPlatform.Tests
{
    public class MemoryStoreTests
    {
        [Fact]
        public async Task SqliteMemoryStore_StoreAndSearch_ReturnsMatchingRecord()
        {
            var store = SqliteMemoryStore.CreateInMemory();
            var userId = "user_456";

            await store.StoreAsync(userId, "framework_choice", "ASP.NET Core WebAPI");
            await store.StoreAsync(userId, "database_choice", "PostgreSQL pgvector");

            var results = await store.SearchAsync(new MemoryQuery(userId, "PostgreSQL"));

            Assert.Single(results);
            Assert.Equal("database_choice", results[0].Key);
            Assert.Contains("PostgreSQL", results[0].Value);
        }

        [Fact]
        public async Task SqliteMemoryStore_MultiTenant_IsolatesUserData()
        {
            var store = SqliteMemoryStore.CreateInMemory();
            await store.StoreAsync("tenant_A", "config", "Tenant A secret");
            await store.StoreAsync("tenant_B", "config", "Tenant B secret");

            var resultsA = await store.SearchAsync(new MemoryQuery("tenant_A", "Tenant"));

            Assert.Single(resultsA);
            Assert.Equal("Tenant A secret", resultsA[0].Value);
        }
    }
}
