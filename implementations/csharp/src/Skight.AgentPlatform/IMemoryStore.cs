using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Skight.AgentPlatform
{
    public record MemoryQuery(string UserId, string SearchText, float[]? Vector = null, int Limit = 5);
    public record MemoryRecord(string Key, string Value, float Score);

    public interface IMemoryStore
    {
        Task StoreAsync(string userId, string key, string value);
        Task<IReadOnlyList<MemoryRecord>> SearchAsync(MemoryQuery query);
    }

    public class SqliteMemoryStore : IMemoryStore, IDisposable
    {
        private readonly SqliteConnection _connection;

        public SqliteMemoryStore(string connectionString)
        {
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            InitDb();
        }

        private void InitDb()
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS AgentMemory (
                    UserId TEXT NOT NULL,
                    MemoryKey TEXT NOT NULL,
                    MemoryValue TEXT NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (UserId, MemoryKey)
                );
            ";
            cmd.ExecuteNonQuery();
        }

        public async Task StoreAsync(string userId, string key, string value)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO AgentMemory (UserId, MemoryKey, MemoryValue)
                VALUES (@userId, @key, @value)
                ON CONFLICT(UserId, MemoryKey) DO UPDATE SET MemoryValue = excluded.MemoryValue;
            ";
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@value", value);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IReadOnlyList<MemoryRecord>> SearchAsync(MemoryQuery query)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                SELECT MemoryKey, MemoryValue
                FROM AgentMemory
                WHERE UserId = @userId AND (MemoryKey LIKE @pattern OR MemoryValue LIKE @pattern)
                LIMIT @limit;
            ";
            cmd.Parameters.AddWithValue("@userId", query.UserId);
            cmd.Parameters.AddWithValue("@pattern", $"%{query.SearchText}%");
            cmd.Parameters.AddWithValue("@limit", query.Limit);

            using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<MemoryRecord>();
            while (await reader.ReadAsync())
            {
                list.Add(new MemoryRecord(reader.GetString(0), reader.GetString(1), 1.0f));
            }
            return list;
        }

        public static SqliteMemoryStore CreateInMemory()
        {
            return new SqliteMemoryStore("Data Source=:memory:");
        }

        public static SqliteMemoryStore CreateLocalSqlite(string dbPath)
        {
            var fullPath = Path.GetFullPath(dbPath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return new SqliteMemoryStore($"Data Source={fullPath}");
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}
