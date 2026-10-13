using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Messaging.Dapper.Tests.Integration.Infrastructure;

public sealed record OutboxSeed
{
    public string MessageId { get; init; } = Guid.NewGuid().ToString();
    public string MessageType { get; init; } = "TestMessage";
    public string Topic { get; init; } = "topic";
    public string Key { get; init; } = "key";
    public byte[] Payload { get; init; } = [1, 2, 3];
    public Dictionary<string, string> Headers { get; init; } = [];
    public string Status { get; init; } = "PENDING";
    public int Attempts { get; init; }
    public DateTimeOffset? ProcessedAt { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
}

public sealed class OutboxStoredRow
{
    public long Id { get; init; }
    public string MessageId { get; init; } = "";
    public string MessageType { get; init; } = "";
    public string Topic { get; init; } = "";
    public string MessageKey { get; init; } = "";
    public byte[] Payload { get; init; } = [];
    public string Headers { get; init; } = "";
    public string Status { get; init; } = "";
    public int Attempts { get; init; }
    public DateTimeOffset? ProcessedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class OutboxTable(string connectionString)
{
    public async Task ClearAsync()
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.ExecuteAsync("DELETE FROM outbox");
    }

    public async Task<long> SeedAsync(OutboxSeed seed)
    {
        await using var connection = new SqlConnection(connectionString);
        return await connection.ExecuteScalarAsync<long>("""
            INSERT INTO outbox (message_id, message_type, topic, message_key, payload, headers, status, attempts,
                                processed_at, created_at)
            OUTPUT inserted.id
            VALUES (@MessageId, @MessageType, @Topic, @Key, @Payload, @Headers, @Status, @Attempts,
                    @ProcessedAt, COALESCE(@CreatedAt, SYSDATETIMEOFFSET()))
            """,
            new
            {
                seed.MessageId, seed.MessageType, seed.Topic, seed.Key, seed.Payload,
                Headers = JsonSerializer.Serialize(seed.Headers), seed.Status, seed.Attempts, seed.ProcessedAt,
                seed.CreatedAt
            });
    }

    public async Task<OutboxStoredRow?> GetAsync(long id)
    {
        await using var connection = new SqlConnection(connectionString);
        return await connection.QuerySingleOrDefaultAsync<OutboxStoredRow>(Select + " WHERE id = @Id", new { Id = id });
    }

    public async Task<List<OutboxStoredRow>> GetAllAsync()
    {
        await using var connection = new SqlConnection(connectionString);
        return (await connection.QueryAsync<OutboxStoredRow>(Select + " ORDER BY id")).ToList();
    }

    private const string Select = """
                                  SELECT id AS Id, message_id AS MessageId, message_type AS MessageType, topic AS Topic,
                                         message_key AS MessageKey, payload AS Payload, headers AS Headers,
                                         status AS Status, attempts AS Attempts, processed_at AS ProcessedAt,
                                         created_at AS CreatedAt
                                  FROM outbox
                                  """;
}
