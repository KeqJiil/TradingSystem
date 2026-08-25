using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Commands.UpdateReadModel;

public class UpdateReadModelHandler(IStockReadModelWriter writer) : IRequestHandler<UpdateReadModelCommand>
{
    public async Task Handle(UpdateReadModelCommand request, CancellationToken cancellationToken)
    {
        await writer.UpdateAsync(request.AggregateId, request.Version, request.PriceChange, cancellationToken);
    }
}