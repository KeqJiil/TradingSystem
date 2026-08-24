using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Services;

public class StockDataService
{
    private readonly IStockWriter _stockWriter;
    private readonly IUnitOfWorkDecorator _unitOfWork;
    private readonly IOutboxWriter _outboxWriter;
    
    public StockDataService(IStockWriter stockWriter, IUnitOfWorkDecorator unitOfWork, IOutboxWriter outboxWriter)
    {
        _stockWriter = stockWriter;
        _unitOfWork = unitOfWork;
        _outboxWriter = outboxWriter;
    }
    
    public async Task<Guid> CreateAsync(CreateStockDto request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await _stockWriter.CreateAsync(id, request, cancellationToken);
        
        return id;
    }
    
    public Task ChangeNameAsync(Guid stockId, string name, CancellationToken cancellationToken)
    {
        return _stockWriter.ChangeName(stockId, name, cancellationToken);
    }
    
    public Task ChangeTimeAsync(Guid stockId, TimeOnly openTime, TimeOnly closeTime, CancellationToken cancellationToken)
    {
        return _stockWriter.ChangeTime(stockId, openTime, closeTime, cancellationToken);
    }
    
    public async Task ToggleOpenToTradeAsync(Guid stockId, CancellationToken cancellationToken)
    {
        await _unitOfWork.ExecuteAsync(async () =>
        {
            await _stockWriter.ToggleOpenToTrade(stockId, cancellationToken);
            await _outboxWriter.WriteAsync(new StockToggledStatusEvent(stockId), stockId, cancellationToken);
        }, cancellationToken);
    }
}