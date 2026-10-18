using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Messaging.EFCore.Inbox;

internal class InboxHook(InboxState state) : ISaveChangesInterceptor
{
    public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = new())
    {
        if (string.IsNullOrEmpty(state.MessageId) || string.IsNullOrEmpty(state.ConsumerGroup) || state.Committed)
            return new ValueTask<InterceptionResult<int>>(result);

        if (!state.Added)
        {
            eventData.Context?.Set<InboxEntity>().Add(new InboxEntity
                { MessageId = state.MessageId, ConsumerGroup = state.ConsumerGroup });
            state.Added = true;
        }

        return new ValueTask<InterceptionResult<int>>(result);
    }

    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (string.IsNullOrEmpty(state.MessageId) || string.IsNullOrEmpty(state.ConsumerGroup) || state.Committed)
            return result;

        if (!state.Added)
        {
            eventData.Context?.Set<InboxEntity>().Add(new InboxEntity
                { MessageId = state.MessageId, ConsumerGroup = state.ConsumerGroup });
            state.Added = true;
        }

        return result;
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        state.Commit();
        return result;
    }

    public ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = new())
    {
        state.Commit();
        return new ValueTask<int>(result);
    }
}