using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Commands.UpdateStock;

public class UpdateStockHandler : IRequestHandler<UpdateStockCommand, Guid>
{
    private readonly IStockEventStore _stockEventStore;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateStockHandler(IStockEventStore stockEventStore, IUnitOfWork unitOfWork)
    {
        _stockEventStore = stockEventStore;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(UpdateStockCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.StartTransactionAsync(cancellationToken);
        
        var stockUpdateEvent = new PriceChangeEvent(request.AggregateId, request.PriceChange);
        await _stockEventStore.AppendAsync(stockUpdateEvent, cancellationToken);
        
        await _unitOfWork.CommitAsync(cancellationToken);
        
        return request.AggregateId;
    }
}