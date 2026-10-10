using Messaging.Abstractions;
using Messaging.Dapper.DbTasks;

namespace Messaging.Dapper.Inbox;

internal class InboxMiddleware(DbTaskQueue queue, InboxWriter writer, ITransactionContext db)
    : IMessageDeliveryMiddleware
{
    public async Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context,
        DeliveryDelegate next, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(context.MessageId))
            return new ConsumeOutcome(MessageConsumeResult.DeadLetter, "missing message id");

        queue.Register("inbox", (connection, transaction, token) =>
            writer.TryInsertAsync(connection, transaction, context.ConsumerGroup, context.MessageId, token));

        var outcome = await next();

        if (queue.Vetoed) return new ConsumeOutcome(MessageConsumeResult.Success);
        if (outcome.Kind != MessageConsumeResult.Success || !queue.HasPending) return outcome;

        await db.EnsureConnectionOpenAsync(cancellationToken);
        return await queue.RunPendingAsync(db.Connection, db.Transaction, cancellationToken)
            ? outcome
            : new ConsumeOutcome(MessageConsumeResult.Success);
    }
}
