using Messaging.Abstractions;

namespace Order.Infrastructure.Messaging.Events;

public abstract record ExternalEvent<TSelf> where TSelf : ExternalEvent<TSelf>
{
    public Task<PublishOutcome> PublishWith(IMessagePublisher publisher, PublishOptions options, CancellationToken cancellationToken)
    {
        return publisher.PublishAsync((TSelf)this, options, cancellationToken);
    }
}
