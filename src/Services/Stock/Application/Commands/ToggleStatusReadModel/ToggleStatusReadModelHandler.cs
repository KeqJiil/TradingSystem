using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Commands.ToggleStatusReadModel;

public class ToggleStatusReadModelHandler(IStockReadModelWriter writer) : IRequestHandler<ToggleStatusReadModelCommand>
{
    public async Task Handle(ToggleStatusReadModelCommand request, CancellationToken cancellationToken)
    {
        await writer.ToggleStatusAsync(request.AggregateId, cancellationToken);
    }
}