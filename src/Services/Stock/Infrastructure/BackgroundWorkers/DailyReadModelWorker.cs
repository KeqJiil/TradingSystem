using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Infrastructure.BackgroundWorkers;

public class DailyReadModelWorker(
    ILogger<DailyReadModelWorker> logger,
    IStockEventStoreReader eventStoreReader,
    IStockPriceHistoryReader priceHistoryReader,
    IStockDailyReadModelWriter writer) : IJobEventProcessor<DailyReadModelRequested>
{
    public async Task ProcessAsync(DailyReadModelRequested @event, CancellationToken ct)
    {
        var aggregateId = @event.AggregateId;
        var date = @event.Date;

        logger.LogInformation("DailyReadModelWorker started for {AggregateId} on {Date}", aggregateId, date);

        var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = from.AddDays(1);

        var previousDay = await priceHistoryReader.GetDayPriceHistoryAsync(aggregateId, date.AddDays(-1), ct);
        var cumulativePrice = previousDay?.ClosePrice ?? 0m;

        decimal? open = null;
        var high = decimal.MinValue;
        var low = decimal.MaxValue;
        var count = 0;

        await foreach (var priceEvent in eventStoreReader.ListEventsAsync(aggregateId, from, to, ct))
        {
            cumulativePrice += priceEvent.PriceChange;
            open ??= cumulativePrice;
            if (cumulativePrice > high) high = cumulativePrice;
            if (cumulativePrice < low) low = cumulativePrice;
            count++;
        }

        if (count == 0)
        {
            logger.LogInformation("No events for {AggregateId} on {Date}, skipping", aggregateId, date);
            return;
        }

        var lastVersion = await eventStoreReader.GetLastVersionAsync(aggregateId, to, ct) ?? 0;

        await writer.CreateDailyReadModelAsync(
            new DailyReadModelAggregate(aggregateId, date, open.Value, low, high, cumulativePrice,
                cumulativePrice - open.Value, lastVersion),
            ct);

        logger.LogInformation("DailyReadModelWorker finished for {AggregateId} on {Date}", aggregateId, date);
    }
}
