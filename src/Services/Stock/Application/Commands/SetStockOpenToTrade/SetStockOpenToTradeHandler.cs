using MediatR;
using Messaging.Abstractions;
using Messaging.Dapper.Transactions;
using Stock.Application.Abstractions;
using Stock.Application.Events;
using Stock.Application.Services;

namespace Stock.Application.Commands.SetStockOpenToTrade;

public class SetStockOpenToTradeHandler(IStockWriter writer, IMessagePublisher publisher, IUnitOfWorkDecorator uow)
    : IRequestHandler<SetStockOpenToTradeCommand, bool>
{
    public async Task<bool> Handle(SetStockOpenToTradeCommand request, CancellationToken cancellationToken)
    {
        var found = false;

        await uow.ExecuteAsync(async () =>
        {
            var version = await writer.SetOpenToTrade(request.Id, request.IsOpenToTrade, cancellationToken);
            found = version is not null;
            if (!found) return;

            var @event = new StockToggledStatusEvent(request.Id, DateTimeOffset.UtcNow, request.IsOpenToTrade,
                version!.Value);
            await publisher.PublishOrThrowAsync(@event, request.Id, cancellationToken);
        }, cancellationToken);

        return found;
    }
}
