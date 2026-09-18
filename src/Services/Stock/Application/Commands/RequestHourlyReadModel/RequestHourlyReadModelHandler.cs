using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Commands.RequestHourlyReadModel;

public class RequestHourlyReadModelHandler(IOutboxWriter outboxWriter, IStockDataReader stockDataReader)
    : IRequestHandler<RequestHourlyReadModelCommand>
{
    public async Task Handle(RequestHourlyReadModelCommand request, CancellationToken cancellationToken)
    {
        const int batchSize = 100;
        var batch = new List<(Guid, HourlyReadModelRequested)>(batchSize);

        await foreach (var stockId in stockDataReader.GetAllIdsAsync(batchSize, cancellationToken))
        {
            batch.Add((stockId, new HourlyReadModelRequested(stockId, request.Date, request.Hour)));
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