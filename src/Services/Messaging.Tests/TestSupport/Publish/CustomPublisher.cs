namespace Messaging.Tests.TestSupport.Publish;

public sealed class CustomPublisher : IMessagePublisher
{
    public Task<PublishOutcome> PublishAsync<TMessage>(TMessage message, PublishOptions? options,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new PublishOutcome(true));
    }
}
