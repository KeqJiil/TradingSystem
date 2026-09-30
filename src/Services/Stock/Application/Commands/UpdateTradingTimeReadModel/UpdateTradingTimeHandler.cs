using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Exceptions;

namespace Stock.Application.Commands.UpdateTradingTimeReadModel;

public class UpdateTradingTimeHandler(IStockReadModelWriter writer) : IRequestHandler<UpdateTradingTimeCommand>
{
    public async Task Handle(UpdateTradingTimeCommand request, CancellationToken cancellationToken)
    {
        var exists = await writer.SetNewTimeAsync(request.AggregateId, request.TradingStartTime,
            request.TradingCloseTime, request.Version, cancellationToken);

        if (!exists) throw new ReadModelNotFoundException(request.AggregateId);
    }
}
