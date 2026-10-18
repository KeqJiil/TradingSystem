using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Infrastructure.BackgroundWorkers;

public class DailyReadModelWorker(
    ILogger<DailyReadModelWorker> logger,
    IStockEventStoreReader eventStoreReader,
    IStockPriceHourlyReader hourlyReader,
    IStockDailyReadModelWriter writer) : IJobEventProcessor<DailyReadModelRequested>
{
    public async Task ProcessAsync(DailyReadModelRequested @event, CancellationToken ct)
    {
        var aggregateId = @event.AggregateId;
        var date = @event.Date;

        logger.LogDebug("DailyReadModelWorker started for {AggregateId} on {Date}", aggregateId, date);

        var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = from.AddDays(1);

        decimal? open = null;
        var close = 0m;
        var high = decimal.MinValue;
        var low = decimal.MaxValue;

        foreach (var priceEvent in await hourlyReader.GetHourlyPriceHistoryAsync(aggregateId, from, dayEnd, ct))
        {
            open ??= priceEvent.OpenPrice;
            close = priceEvent.ClosePrice;
            if (priceEvent.HighPrice > high) high = priceEvent.HighPrice;
            if (priceEvent.LowPrice < low) low = priceEvent.LowPrice;
        }

        if (open is null)
        {
            logger.LogDebug("No events for {AggregateId} on {Date}, skipping", aggregateId, date);
            return;
        }

        var lastVersion = await eventStoreReader.GetLastVersionAsync(aggregateId, dayEnd, ct) ?? 0;

        await writer.CreateDailyReadModelAsync(
            new DailyReadModelAggregate(aggregateId, date, open.Value, low, high, close,
                close - open.Value, lastVersion),
            ct);

        logger.LogDebug("DailyReadModelWorker finished for {AggregateId} on {Date}", aggregateId, date);
    }
}