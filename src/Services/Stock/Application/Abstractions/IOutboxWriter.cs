namespace Stock.Application.Abstractions;

public interface IOutboxWriter
{
    Task WriteAsync<T>(T @event, Guid aggregateId, CancellationToken cancellationToken) where T : class;
}