using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Commands.ToggleStockOpenToTrade;

public class ToggleStockOpenToTradeHandler(IStockWriter writer) : IRequestHandler<ToggleStockOpenToTradeCommand>
{
    public async Task Handle(ToggleStockOpenToTradeCommand request, CancellationToken cancellationToken)
    {
        await writer.ToggleOpenToTrade(request.Id, cancellationToken);
    }
}
