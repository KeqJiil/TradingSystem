using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Commands.CreateStock;

public class CreateStockHandler : IRequestHandler<CreateStockCommand, Guid>
{
    private readonly IStockEventStore _stockEventStore;

    public CreateStockHandler(IStockEventStore stockEventStore)
    {
        _stockEventStore = stockEventStore;
    }

    public async Task<Guid> Handle(CreateStockCommand request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var stockUpdateEvent = new StockCreateEvent(id, request.Name, request.IsOpenToTrade, request.Currency, request.OpenTime, request.CloseTime);
        await _stockEventStore.AppendAsync(stockUpdateEvent, cancellationToken);
        
        return id;
    }
}