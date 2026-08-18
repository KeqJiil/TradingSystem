using Stock.Application.Abstractions;

namespace Stock.Application.Events;

public sealed record StockCreateEvent(
    Guid Id,
    string Name, 
    bool IsOpenToTrade,
    string Currency,
    TimeOnly TradingStartTime,
    TimeOnly TradingCloseTime) : StockEvent();