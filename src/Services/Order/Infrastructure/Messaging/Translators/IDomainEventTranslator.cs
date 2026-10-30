using Order.Domain.Events;
using Order.Infrastructure.Messaging.Events;

namespace Order.Infrastructure.Messaging.Translators;

public interface IDomainEventTranslator
{
    IExternalEvent? Translate(DomainEvent domainEvent);
}
