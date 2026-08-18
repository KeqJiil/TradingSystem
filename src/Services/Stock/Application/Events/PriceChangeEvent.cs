using Stock.Application.Abstractions;

namespace Stock.Application.Events;

public sealed record PriceChangeEvent(Guid AggregateId, decimal PriceChange) : StockEvent();