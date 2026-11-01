using Messaging.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Order.Domain;
using Order.Infrastructure.Messaging.Translators;

namespace Order.Infrastructure.Persistence.Interceptors;

public class MessagePublishingInterceptor(IServiceProvider sp, IDomainEventTranslator translator) : ISaveChangesInterceptor
{
    public async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = new())
    {
        var publisher = sp.GetRequiredService<IMessagePublisher>();
        
        var context = eventData.Context;
        if (context is null)
            return result;

        var aggregates = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .Where(a => a.GetDomainEvents().Count > 0)
            .ToList();

        
        var messages = aggregates.SelectMany(a => a.GetDomainEvents()).ToList();

        aggregates.ForEach(a => a.ClearDomainEvents());
        
        foreach (var message in messages)
        {
            var externalEvent = translator.Translate(message);
            if (externalEvent is null)
            {
                continue;
            }
            
            var publishOutcome = await externalEvent.PublishWith(publisher, new PublishOptions(externalEvent.AggregateId.ToString()), cancellationToken);

            if (!publishOutcome.IsSuccessful)
                throw new Exception(publishOutcome.ErrorMessage);
        }


        return result;
    }
}