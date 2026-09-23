using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Commands.ChangeStockName;

public class ChangeStockNameHandler(IStockWriter writer, IOutboxWriter outboxWriter, IUnitOfWorkDecorator uow)
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

            await outboxWriter.WriteAsync(new NameChangedEvent(request.Id, request.Name, version!.Value), request.Id,
                cancellationToken);
        }, cancellationToken);

        return found;
    }
}
