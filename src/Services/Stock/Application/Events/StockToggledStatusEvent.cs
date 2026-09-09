namespace Stock.Application.Events;

public record StockToggledStatusEvent(Guid AggregateId, DateTimeOffset ToggledAt) : StockEvent(AggregateId);