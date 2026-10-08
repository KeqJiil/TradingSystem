namespace Messaging.IntegrationTests.Support;

public sealed class DeliveryCaptureLog
{
    private readonly List<object> _contexts = new();

    public IReadOnlyList<DeliveryContext<TMessage>> For<TMessage>()
    {
        lock (_contexts) return _contexts.OfType<DeliveryContext<TMessage>>().ToList();
    }

    internal void Add(object context)
    {
        lock (_contexts) _contexts.Add(context);
    }
}

public sealed class CaptureDeliveryMiddleware(DeliveryCaptureLog log) : IMessageDeliveryMiddleware
{
    public Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context, DeliveryDelegate next,
        CancellationToken cancellationToken)
    {
        log.Add(context);
        return next();
    }
}
