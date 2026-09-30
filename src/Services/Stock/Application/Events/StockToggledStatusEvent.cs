namespace Stock.Application.Events;

public record StockToggledStatusEvent(
    Guid AggregateId,
    DateTimeOffset ToggledAt,
    bool IsOpenToTrade,
    long StatusVersion) : StockEvent(AggregateId);
