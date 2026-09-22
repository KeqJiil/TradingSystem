using MediatR;
using Microsoft.Extensions.Internal;
using Stock.Application.Abstractions;
using Stock.Application.Exceptions;

namespace Stock.Application.Commands.ReplayReadModel;

public class ReplayReadModelHandler(
    IStockEventStoreReader reader,
    IStockReader readModelReader,
    ISystemClock clock,
    IStockReadModelWriter writer)
    : IRequestHandler<ReplayReadModelCommand>
{
    public async Task Handle(ReplayReadModelCommand request, CancellationToken cancellationToken)
    {
        var appliedVersion = await readModelReader.GetVersionAsync(request.AggregateId, cancellationToken);
        if (appliedVersion is null) throw new ReadModelNotFoundException(request.AggregateId);

        var lastStoredVersion =
            await reader.GetLastVersionAsync(request.AggregateId, clock.UtcNow, cancellationToken) ?? 0;

        var toVersion = Math.Min(lastStoredVersion, request.MaxVersion);
        if (toVersion <= appliedVersion.Value) return;

        var events = (await reader.ListEventsByVersionAsync(request.AggregateId, appliedVersion.Value, toVersion,
            cancellationToken)).ToArray();

        if (events.Length == 0) return;

        var totalPriceChange = events.Sum(e => e.PriceChange);

        await writer.ReplayAsync(request.AggregateId, appliedVersion.Value, events[^1].Version!.Value, totalPriceChange,
            cancellationToken);
    }
}
