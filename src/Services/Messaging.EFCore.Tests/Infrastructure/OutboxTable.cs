using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Messaging.EFCore.Tests.Infrastructure;

public sealed class OutboxTable(string connectionString) : IOutboxTable
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
            INSERT INTO outbox (id, message_id, message_type, topic, message_key, payload, headers, status, attempts,
                                processed_at, created_at)
            OUTPUT inserted.id
            VALUES (NEXT VALUE FOR outbox_id_seq, @MessageId, @MessageType, @Topic, @Key, @Payload, @Headers, @Status,
                    @Attempts, @ProcessedAt, COALESCE(@CreatedAt, SYSDATETIMEOFFSET()))
            """,
            new
            {
                seed.MessageId, seed.MessageType, seed.Topic, seed.Key, seed.Payload,
                Headers = JsonSerializer.Serialize(seed.Headers), Status = ToEfStatus(seed.Status), seed.Attempts,
                seed.ProcessedAt, seed.CreatedAt
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

    private static string ToEfStatus(string status) => status[..1] + status[1..].ToLowerInvariant();

    private const string Select = """
                                  SELECT id AS Id, message_id AS MessageId, message_type AS MessageType, topic AS Topic,
                                         message_key AS MessageKey, payload AS Payload, headers AS Headers,
                                         UPPER(status) AS Status, attempts AS Attempts, processed_at AS ProcessedAt,
                                         created_at AS CreatedAt
                                  FROM outbox
                                  """;
}
