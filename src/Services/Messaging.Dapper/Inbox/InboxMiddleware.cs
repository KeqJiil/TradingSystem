using Messaging.Abstractions;
using Messaging.Dapper.DbTasks;
using Microsoft.Extensions.Logging;

namespace Messaging.Dapper.Inbox;

internal class InboxMiddleware(
    DbTaskQueue queue,
    InboxWriter writer,
    ITransactionContext db,
    ILogger<InboxMiddleware> logger) : IMessageDeliveryMiddleware
{
    private const string InboxTask = "inbox";

    public async Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context,
        DeliveryDelegate next, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(context.MessageId))
            return new ConsumeOutcome(MessageConsumeResult.DeadLetter, "missing message id");

        queue.Register(InboxTask, (connection, transaction, token) =>
            writer.TryInsertAsync(connection, transaction, context.ConsumerGroup, context.MessageId, token));

        var outcome = await next();

        if (queue.Vetoed) return new ConsumeOutcome(MessageConsumeResult.Success);

        if (outcome.Kind != MessageConsumeResult.Success)
        {
            if (queue.IsCommitted(InboxTask))
                logger.LogError(
                    "Consumer of {MessageType} committed message {MessageId} in group {ConsumerGroup} but returned {Outcome}; the message is already marked as processed, so a redelivery will be skipped",
                    context.MessageType, context.MessageId, context.ConsumerGroup, outcome.Kind);
            return outcome;
        }

        if (!queue.HasPending) return outcome;

        await db.EnsureConnectionOpenAsync(cancellationToken);
        return await queue.RunPendingAsync(db.Connection, db.Transaction, cancellationToken)
            ? outcome
            : new ConsumeOutcome(MessageConsumeResult.Success);
    }
}
