using Messaging.Abstractions;

namespace Order.Infrastructure.Messaging.Events;

public interface IExternalEvent
{
    Guid AggregateId { get; }
    Task<PublishOutcome> PublishWith(IMessagePublisher publisher, PublishOptions options, CancellationToken cancellationToken);
}
