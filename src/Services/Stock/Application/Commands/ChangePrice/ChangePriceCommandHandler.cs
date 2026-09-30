using MediatR;
using Stock.Application.Events;
using Stock.Application.Services;

namespace Stock.Application.Commands.ChangePrice;

public class ChangePriceCommandHandler(EventStoreService eventStoreService) : IRequestHandler<ChangePriceCommand>
{
    public async Task Handle(ChangePriceCommand request, CancellationToken cancellationToken)
    {
        var priceChangeRequested = new PriceChangeRequested(request.EventId, request.AggregateId, request.PriceChange,
            request.OccuredAt);

        await eventStoreService.ChangePriceAppendAsync(priceChangeRequested, cancellationToken);
    }
}
