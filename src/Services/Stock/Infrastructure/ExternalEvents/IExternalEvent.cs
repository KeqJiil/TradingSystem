namespace Stock.Infrastructure.ExternalEvents;

public interface IExternalEvent
{
    Guid AggregateId { get; }
}
