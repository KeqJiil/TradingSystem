using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Commands.CreateReadModel;

public class CreateReadModelHandler(IStockReadModelWriter writer) : IRequestHandler<CreateReadModelCommand>
{
    public async Task Handle(CreateReadModelCommand request, CancellationToken ct)
    {
        var dto = new CreateStockReadModelDto(
            request.AggregateId, request.Name, 
            request.IsOpenToTrade, request.Currency, 
            request.TradingStartTime, request.TradingCloseTime);
        
        await writer.CreateAsync(dto ,ct);
    }
}