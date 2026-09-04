using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Commands.CreateDailyReadModel;

public class CreateDailyReadModelHandler(IOutboxWriter outboxWriter, IStockDataReader stockDataReader)
    : IRequestHandler<CreateDailyReadModelCommand>
{
    public async Task Handle(CreateDailyReadModelCommand request, CancellationToken cancellationToken)
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