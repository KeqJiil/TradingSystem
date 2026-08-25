namespace Stock.Application.Events;

public sealed record PriceChangedEvent(Guid AggregateId, decimal PriceChange, long? Version) : StockEvent(AggregateId);