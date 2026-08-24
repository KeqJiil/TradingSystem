namespace Stock.Application.Events;

public abstract record StockEvent(Guid AggregateId);