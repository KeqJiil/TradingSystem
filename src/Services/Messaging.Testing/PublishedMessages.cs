using System.Text.Json;
using Messaging.Abstractions;
using Microsoft.Data.SqlClient;

namespace Messaging.Testing;

public sealed record PublishedMessage<TMessage>(
    TMessage Message,
    string Topic,
    string Key,
    IReadOnlyDictionary<string, string> Headers);

public sealed class PublishedMessages(string connectionString, IMessageSerializerResolver serializers)
{
    public async Task<List<PublishedMessage<TMessage>>> OfTypeAsync<TMessage>(string? key = null)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            """
            SELECT topic, message_key, payload, headers
            FROM messaging.outbox
            WHERE message_type = @Type AND (@Key IS NULL OR message_key = @Key)
            ORDER BY id
            """, connection);
        command.Parameters.AddWithValue("@Type", typeof(TMessage).Name);
        command.Parameters.AddWithValue("@Key", (object?)key ?? DBNull.Value);

        var serializer = serializers.For<TMessage>();
        var result = new List<PublishedMessage<TMessage>>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var deserialized = serializer.Deserialize<TMessage>((byte[])reader["payload"]);
            if (!deserialized.Success)
                throw new InvalidOperationException(
                    $"Cannot deserialize published {typeof(TMessage).Name}: {deserialized.Error}");

            result.Add(new PublishedMessage<TMessage>(
                deserialized.Message,
                reader.GetString(0),
                reader.GetString(1),
                JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(3)) ?? []));
        }

        return result;
    }
}
