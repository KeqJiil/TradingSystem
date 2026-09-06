using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Commands.CreateStock;

public class CreateStockHandler(IStockWriter writer) : IRequestHandler<CreateStockCommand, Guid>
{
    public async Task<Guid> Handle(CreateStockCommand request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();

        var dto = new CreateStockDto(
            request.Name,
            request.IsOpenToTrade,
            request.TradingStartTime,
            request.TradingEndTime,
            request.Currency);

        await writer.CreateAsync(id, dto, cancellationToken);

        return id;
    }
}
