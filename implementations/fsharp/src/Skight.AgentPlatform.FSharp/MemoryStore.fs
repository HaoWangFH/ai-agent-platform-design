namespace Skight.AgentPlatform.FSharp

open System
open System.IO
open System.Threading.Tasks
open Microsoft.Data.Sqlite

type MemoryQuery = {
    UserId: string
    SearchText: string
    Vector: float32[] option
    Limit: int
}

type MemoryRecord = {
    Key: string
    Value: string
    Score: float32
}

type IMemoryStore =
    abstract member StoreAsync: userId: string -> key: string -> value: string -> Async<unit>
    abstract member SearchAsync: query: MemoryQuery -> Async<MemoryRecord list>

module MemoryStoreFactory =

    type SqliteMemoryStore(connectionString: string) =
        let connStr = if connectionString.Contains("=") then connectionString else sprintf "Data Source=%s" connectionString
        let conn = new SqliteConnection(connStr)
        do conn.Open()

        let initDb () =
            use cmd = conn.CreateCommand()
            cmd.CommandText <- """
                CREATE TABLE IF NOT EXISTS AgentMemory (
                    UserId TEXT NOT NULL,
                    MemoryKey TEXT NOT NULL,
                    MemoryValue TEXT NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (UserId, MemoryKey)
                );
            """
            cmd.ExecuteNonQuery() |> ignore

        do initDb()

        interface IMemoryStore with
            member _.StoreAsync(userId: string) (key: string) (value: string) =
                async {
                    use cmd = conn.CreateCommand()
                    cmd.CommandText <- """
                        INSERT INTO AgentMemory (UserId, MemoryKey, MemoryValue)
                        VALUES (@userId, @key, @value)
                        ON CONFLICT(UserId, MemoryKey) DO UPDATE SET MemoryValue = excluded.MemoryValue;
                    """
                    cmd.Parameters.AddWithValue("@userId", userId) |> ignore
                    cmd.Parameters.AddWithValue("@key", key) |> ignore
                    cmd.Parameters.AddWithValue("@value", value) |> ignore
                    let! _ = cmd.ExecuteNonQueryAsync() |> Async.AwaitTask
                    return ()
                }

            member _.SearchAsync(query: MemoryQuery) =
                async {
                    use cmd = conn.CreateCommand()
                    cmd.CommandText <- """
                        SELECT MemoryKey, MemoryValue
                        FROM AgentMemory
                        WHERE UserId = @userId AND (MemoryKey LIKE @pattern OR MemoryValue LIKE @pattern)
                        LIMIT @limit;
                    """
                    cmd.Parameters.AddWithValue("@userId", query.UserId) |> ignore
                    cmd.Parameters.AddWithValue("@pattern", sprintf "%%%s%%" query.SearchText) |> ignore
                    cmd.Parameters.AddWithValue("@limit", query.Limit) |> ignore
                    
                    use! reader = cmd.ExecuteReaderAsync() |> Async.AwaitTask
                    let results = System.Collections.Generic.List<MemoryRecord>()
                    while reader.Read() do
                        results.Add({
                            Key = reader.GetString(0)
                            Value = reader.GetString(1)
                            Score = 1.0f
                        })
                    return List.ofSeq results
                }

        interface IDisposable with
            member _.Dispose() =
                conn.Dispose()

    let createInMemory () : IMemoryStore =
        new SqliteMemoryStore("Data Source=:memory:") :> IMemoryStore

    let createLocalSqlite (dbPath: string) : IMemoryStore =
        let fullPath = Path.GetFullPath(dbPath)
        let dir = Path.GetDirectoryName(fullPath)
        if not (String.IsNullOrEmpty(dir)) && not (Directory.Exists(dir)) then
            Directory.CreateDirectory(dir) |> ignore
        new SqliteMemoryStore(sprintf "Data Source=%s" fullPath) :> IMemoryStore
