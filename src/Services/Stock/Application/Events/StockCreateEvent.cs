namespace Stock.Application.Events;

public sealed record StockCreateEvent(
    Guid AggregateId,
    string Name, 
    bool IsOpenToTrade,
    string Currency,
    TimeOnly TradingStartTime,
    TimeOnly TradingCloseTime) : StockEvent(AggregateId);