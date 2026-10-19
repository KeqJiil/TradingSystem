using MediatR;
using Messaging.Abstractions;
using Stock.Application.Abstractions;
using Stock.Application.Events;
using IUnitOfWorkDecorator = Messaging.Dapper.Transactions.IUnitOfWorkDecorator;

namespace Stock.Application.Commands.ChangeStockTradingTime;

public class ChangeStockTradingTimeHandler(IStockWriter writer, IMessagePublisher publisher, IUnitOfWorkDecorator uow)
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

            await publisher.PublishAsync(
                new TimeChangedEvent(request.Id, request.OpenTime, request.CloseTime, version!.Value), new PublishOptions { KeyId = request.Id.ToString() }, cancellationToken);
        }, cancellationToken);

        return found;
    }
}
