namespace Stock.Application.Events;

public record TimeChangedEvent(
    Guid AggregateId,
    TimeOnly TradingStartTime,
    TimeOnly TradingCloseTime,
    long Version) : StockEvent(AggregateId);