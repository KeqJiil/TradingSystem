using MediatR;
using Microsoft.Extensions.Internal;
using Stock.Application.Abstractions;

namespace Stock.Application.Commands.ReplayReadModel;

public class ReplayReadModelHandler(IStockEventStoreReader reader, ISystemClock clock, IStockReadModelWriter writer)
    : IRequestHandler<ReplayReadModelCommand>
{
    public async Task Handle(ReplayReadModelCommand request, CancellationToken cancellationToken)
    {
        var lastActualVersion = await reader.GetLastVersionAsync(request.AggregateId, clock.UtcNow, cancellationToken) ?? 0;

        var events = (await reader.ListEventsByVersionAsync(request.AggregateId, request.MaxVersion,
            lastActualVersion, cancellationToken)).ToArray();
        
        if (events.Length == 0) return;
        
        var totalPriceChange = events.Sum(e => e.PriceChange);
        var toVersion = events[^1].Version!.Value;
        
        await writer.ReplayAsync(request.AggregateId, lastActualVersion, toVersion, totalPriceChange, cancellationToken);
    }
}