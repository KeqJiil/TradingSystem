namespace Stock.Application.Abstractions;

public interface IOutboxWriter
{
    Task WriteAsync<T>(T @event, CancellationToken cancellationToken) where T : class;
}