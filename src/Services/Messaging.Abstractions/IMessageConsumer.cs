namespace Messaging.Abstractions;

public interface IMessageConsumer<TMessage>
{
    Task ConsumeAsync(TMessage message, CancellationToken cancellationToken);
}