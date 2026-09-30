namespace Stock.Application.Events;

public sealed record PriceChangedEvent(Guid AggregateId, decimal PriceChange, long? Version, DateTimeOffset OccuredAt)
    : StockEvent(AggregateId);
    
public sealed record PriceChangeRequested(Guid EventId, Guid AggregateId, decimal PriceChange, DateTimeOffset OccuredAt) : StockEvent(AggregateId);