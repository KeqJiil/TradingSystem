namespace Stock.Application.Events;

public record NameChangedEvent(
    Guid AggregateId,
    string Name,
    long Version) : StockEvent(AggregateId);