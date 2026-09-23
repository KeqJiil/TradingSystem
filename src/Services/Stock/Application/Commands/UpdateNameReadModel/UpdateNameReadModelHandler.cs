using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Exceptions;

namespace Stock.Application.Commands.UpdateNameReadModel;

public class UpdateNameReadModelHandler(IStockReadModelWriter writer) : IRequestHandler<UpdateNameReadModelCommand>
{
    public async Task Handle(UpdateNameReadModelCommand request, CancellationToken cancellationToken)
    {
        var exists = await writer.SetNewNameAsync(request.AggregateId, request.Name, request.Version,
            cancellationToken);

        if (!exists) throw new ReadModelNotFoundException(request.AggregateId);
    }
}
