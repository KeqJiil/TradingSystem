namespace Stock.Application.Events;

public record StockToggledStatusEvent(Guid AggregateId) : StockEvent(AggregateId);