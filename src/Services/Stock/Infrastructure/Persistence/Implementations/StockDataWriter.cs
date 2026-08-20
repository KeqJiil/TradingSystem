using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockDataWriter(IUnitOfWork unitOfWork) : IStockWriter
{
    
    public Task ChangeName(Guid id, string name, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task CreateAsync(Guid id, CreateStockDto dto, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task ChangeTime(Guid id, TimeOnly openTime, TimeOnly closeTime, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task ToggleOpenToTrade(Guid id, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}