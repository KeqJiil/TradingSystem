using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Commands.UpdateReadModel;

public class UpdateReadModelHandler(IStockReadModelWriter writer)
    : IRequestHandler<UpdateReadModelCommand, ReadModelUpdateOutcome>
{
    public Task<ReadModelUpdateOutcome> Handle(UpdateReadModelCommand request, CancellationToken cancellationToken)
    {
        return writer.UpdateAsync(request.AggregateId, request.Version, request.PriceChange, cancellationToken);
    }
}