using Messaging.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Messaging.EFCore.Inbox;

internal class InboxMiddleware<TContext>(
    InboxState inboxState,
    TContext dbContext,
    ILogger<InboxMiddleware<TContext>> logger)
    : IMessageDeliveryMiddleware where TContext : DbContext
{
    public async Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context,
        DeliveryDelegate next, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(context.MessageId) || string.IsNullOrEmpty(context.ConsumerGroup))
            return new ConsumeOutcome(MessageConsumeResult.DeadLetter, "missing message id");

        if (await dbContext.Set<InboxEntity>()
                .AnyAsync(x => x.MessageId == context.MessageId && x.ConsumerGroup == context.ConsumerGroup,
                    cancellationToken)) return new ConsumeOutcome(MessageConsumeResult.Success);

        inboxState.Set(context.MessageId, context.ConsumerGroup);

        var result = new ConsumeOutcome(MessageConsumeResult.Retry);

        try
        {
            result = await next();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 2627 &&
                                           ex.Entries.Any(e => e.Entity is InboxEntity))
        {
            return new ConsumeOutcome(MessageConsumeResult.Success);
        }

        if (result.Kind != MessageConsumeResult.Success)
        {
            if (inboxState.Committed)
                logger.LogError(
                    "Consumer of {MessageType} committed message {MessageId} in group {ConsumerGroup} but returned {Outcome}; the message is already marked as processed, so a redelivery will be skipped",
                    context.MessageType, context.MessageId, context.ConsumerGroup, result.Kind);
            return result;
        }

        if (inboxState.Committed) return result;

        if (dbContext.ChangeTracker.HasChanges())
            logger.LogWarning(
                "Consumer of {MessageType} returned Success with unsaved changes for message {MessageId}; they are discarded",
                context.MessageType, context.MessageId);

        try
        {
            dbContext.ChangeTracker.Clear();
            inboxState.Added = false;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 2627)
        {
            return result;
        }

        return result;
    }
}