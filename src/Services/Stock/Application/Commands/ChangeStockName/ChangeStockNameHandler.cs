using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Commands.ChangeStockName;

public class ChangeStockNameHandler(IStockWriter writer) : IRequestHandler<ChangeStockNameCommand>
{
    public async Task Handle(ChangeStockNameCommand request, CancellationToken cancellationToken)
    {
        await writer.ChangeName(request.Id, request.Name, cancellationToken);
    }
}
