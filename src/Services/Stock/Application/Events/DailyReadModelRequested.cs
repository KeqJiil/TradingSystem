namespace Stock.Application.Events;

public record DailyReadModelRequested(Guid AggregateId, DateOnly Date) : JobEvent(AggregateId);