using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class OutboxWriter(IUnitOfWork unitOfWork) : IOutboxWriter
{
    public Task WriteAsync<T>(T @event, CancellationToken cancellationToken) where T : class
    {
        throw new NotImplementedException();
    }
}