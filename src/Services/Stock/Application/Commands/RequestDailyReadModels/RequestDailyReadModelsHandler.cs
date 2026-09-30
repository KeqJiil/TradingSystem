using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Commands.RequestDailyReadModels;

public class RequestDailyReadModelsHandler(IOutboxWriter outboxWriter, IStockDataReader stockDataReader)
    : IRequestHandler<RequestDailyReadModelsCommand>
{
    public async Task Handle(RequestDailyReadModelsCommand request, CancellationToken cancellationToken)
    {
        const int batchSize = 100;
        var date = DateOnly.FromDateTime(request.Date);
        var batch = new List<(Guid, DailyReadModelRequested)>(batchSize);

        await foreach (var stockId in stockDataReader.GetAllIdsAsync(batchSize, cancellationToken))
        {
            batch.Add((stockId, new DailyReadModelRequested(stockId, date)));
            if (batch.Count == batchSize)
            {
                await outboxWriter.WriteManyAsync(batch, cancellationToken);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
            await outboxWriter.WriteManyAsync(batch, cancellationToken);
    }
}