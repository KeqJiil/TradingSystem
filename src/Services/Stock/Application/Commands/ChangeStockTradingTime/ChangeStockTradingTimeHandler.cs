using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Commands.ChangeStockTradingTime;

public class ChangeStockTradingTimeHandler(IStockWriter writer) : IRequestHandler<ChangeStockTradingTimeCommand>
{
    public async Task Handle(ChangeStockTradingTimeCommand request, CancellationToken cancellationToken)
    {
        await writer.ChangeTime(request.Id, request.OpenTime, request.CloseTime, cancellationToken);
    }
}
