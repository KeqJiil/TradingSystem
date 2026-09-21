using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Exceptions;

namespace Stock.Application.Commands.ToggleStatusReadModel;

public class ToggleStatusReadModelHandler(IStockReadModelWriter writer) : IRequestHandler<ToggleStatusReadModelCommand>
{
    public async Task Handle(ToggleStatusReadModelCommand request, CancellationToken cancellationToken)
    {
        var exists = await writer.SetStatusAsync(request.AggregateId, request.IsOpenToTrade, request.StatusVersion,
            cancellationToken);

        if (!exists) throw new ReadModelNotFoundException(request.AggregateId);
    }
}
