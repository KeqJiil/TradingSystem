using MediatR;
using Messaging.Abstractions;
using Stock.Application.Abstractions;
using Stock.Application.Events;
using IUnitOfWorkDecorator = Messaging.Dapper.Transactions.IUnitOfWorkDecorator;

namespace Stock.Application.Commands.ChangeStockName;

public class ChangeStockNameHandler(IStockWriter writer, IMessagePublisher publisher, IUnitOfWorkDecorator uow)
    : IRequestHandler<ChangeStockNameCommand, bool>
{
    public async Task<bool> Handle(ChangeStockNameCommand request, CancellationToken cancellationToken)
    {
        var found = false;

        await uow.ExecuteAsync(async () =>
        {
            var version = await writer.ChangeName(request.Id, request.Name, cancellationToken);
            found = version is not null;
            if (!found) return;

            await publisher.PublishAsync(new NameChangedEvent(request.Id, request.Name, version!.Value), new PublishOptions { KeyId = request.Id.ToString() }, cancellationToken);
        }, cancellationToken);

        return found;
    }
}
