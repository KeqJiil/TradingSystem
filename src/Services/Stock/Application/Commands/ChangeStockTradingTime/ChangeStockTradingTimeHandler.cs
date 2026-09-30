using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Commands.ChangeStockTradingTime;

public class ChangeStockTradingTimeHandler(IStockWriter writer, IOutboxWriter outboxWriter, IUnitOfWorkDecorator uow)
    : IRequestHandler<ChangeStockTradingTimeCommand, bool>
{
    public async Task<bool> Handle(ChangeStockTradingTimeCommand request, CancellationToken cancellationToken)
    {
        var found = false;

        await uow.ExecuteAsync(async () =>
        {
            var version = await writer.ChangeTime(request.Id, request.OpenTime, request.CloseTime, cancellationToken);
            found = version is not null;
            if (!found) return;

            await outboxWriter.WriteAsync(
                new TimeChangedEvent(request.Id, request.OpenTime, request.CloseTime, version!.Value), request.Id,
                cancellationToken);
        }, cancellationToken);

        return found;
    }
}
