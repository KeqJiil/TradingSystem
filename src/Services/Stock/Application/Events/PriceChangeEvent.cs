namespace Stock.Application.Events;

public sealed record PriceChangedEvent(Guid AggregateId, decimal PriceChange) : StockEvent(AggregateId);