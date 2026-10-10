using System.Data.Common;
using Dapper;

namespace Messaging.Dapper.Inbox;

internal class InboxWriter
{
    public async Task<bool> TryInsertAsync(DbConnection connection, DbTransaction? transaction,
        string consumerGroup, string messageId, CancellationToken cancellationToken)
    {
        var sql = """
                    INSERT INTO inbox (consumer_group, message_id)
                    VALUES (@ConsumerGroup, @MessageId)
                  """;

        var rows = await connection.ExecuteAsync(new CommandDefinition(sql,
            new { ConsumerGroup = consumerGroup, MessageId = messageId }, transaction,
            cancellationToken: cancellationToken));

        return rows > 0;
    }
}