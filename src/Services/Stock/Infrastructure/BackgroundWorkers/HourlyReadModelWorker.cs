using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Infrastructure.BackgroundWorkers;

public class HourlyReadModelWorker(
    ILogger<HourlyReadModelWorker> logger,
    IStockEventStoreReader eventStoreReader,
    IStockPriceHourlyReader priceHistoryReader,
    IStockHourlyReadModelWriter writer) : IJobEventProcessor<HourlyReadModelRequested>
{
    public async Task ProcessAsync(HourlyReadModelRequested @event, CancellationToken ct)
    {
        var aggregateId = @event.AggregateId;
        var hourStart = new DateTimeOffset(@event.Date.ToDateTime(new TimeOnly(@event.Hour, 0)), TimeSpan.Zero);
        var hourEnd = hourStart.AddHours(1);

        logger.LogInformation("HourlyReadModelWorker started for {AggregateId} on {Date}", aggregateId, hourStart);

        var previousHour = await priceHistoryReader.GetLastHourPriceHistoryAsync(aggregateId, hourStart, ct);
        var gapFrom = previousHour is null ? DateTimeOffset.MinValue : previousHour.Value.DateTime.AddHours(1);
        var gap = await eventStoreReader.SumPriceChangeAsync(aggregateId, gapFrom, hourStart, ct);
        var cumulativePrice = (previousHour?.ClosePrice ?? 0m) + gap;

        decimal? open = null;
        var high = decimal.MinValue;
        var low = decimal.MaxValue;
        var count = 0;

        await foreach (var priceEvent in eventStoreReader.ListEventsAsync(aggregateId, hourStart,
                           hourEnd, ct))
        {
            cumulativePrice += priceEvent.PriceChange;
            open ??= cumulativePrice;
            if (cumulativePrice > high) high = cumulativePrice;
            if (cumulativePrice < low) low = cumulativePrice;
            count++;
        }

        if (count == 0)
        {
            logger.LogInformation("No events for {AggregateId} on {Date}, skipping", aggregateId, hourStart);
            return;
        }

        var lastVersion = await eventStoreReader.GetLastVersionAsync(aggregateId, hourEnd, ct) ?? 0;

        await writer.CreateHourlyReadModelAsync(
            new HourlyReadModelAggregate(aggregateId, hourStart, open.Value, low, high, cumulativePrice,
                cumulativePrice - open.Value, lastVersion),
            ct);

        logger.LogInformation("HourlyReadModelWorker finished for {AggregateId} on {Date}", aggregateId,
            hourStart);
    }
}