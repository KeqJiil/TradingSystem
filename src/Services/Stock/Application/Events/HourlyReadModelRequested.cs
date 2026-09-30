namespace Stock.Application.Events;

public record HourlyReadModelRequested(Guid AggregateId, DateOnly Date, byte Hour) : JobEvent(AggregateId);