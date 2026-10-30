namespace Order.Infrastructure.Messaging.Events;

public interface IExternalEvent
{
    Guid AggregateId { get; }
}
