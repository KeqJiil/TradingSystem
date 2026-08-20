using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockEventStore(IUnitOfWork unitOfWork) : IStockEventStore
{
    public Task AppendAsync(PriceChangeEvent stockEvent, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task AppendAsync(IEnumerable<PriceChangeEvent> stockEvents, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}