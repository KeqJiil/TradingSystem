namespace Stock.Application.Abstractions;

public interface IOutboxWriter
{
    Task WriteAsync<T>(T @event, Guid aggregateId, CancellationToken cancellationToken) where T : class;

    Task WriteManyAsync<T>(IEnumerable<(Guid AggregateId, T Payload)> events, CancellationToken cancellationToken)
        where T : class;
}