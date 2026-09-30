namespace Stock.Application.Exceptions;

public class ReadModelNotFoundException(Guid aggregateId)
    : Exception($"Read model for aggregate {aggregateId} does not exist yet")
{
    public Guid AggregateId { get; } = aggregateId;
}
