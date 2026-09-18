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
        var dateTime = @event.Date.ToDateTime(new TimeOnly(@event.Hour, 0));
        var dateTimeOffset = new DateTimeOffset(dateTime, TimeSpan.Zero);

        logger.LogInformation("HourlyReadModelWorker started for {AggregateId} on {Date}", aggregateId, dateTimeOffset);

        var previousDay = await priceHistoryReader.GetLastHourPriceHistoryAsync(aggregateId, dateTimeOffset, ct);
        var cumulativePrice = previousDay?.ClosePrice ?? 0m;

        decimal? open = null;
        var high = decimal.MinValue;
        var low = decimal.MaxValue;
        var count = 0;

        await foreach (var priceEvent in eventStoreReader.ListEventsAsync(aggregateId, dateTimeOffset.AddHours(-1),
                           dateTimeOffset, ct))
        {
            cumulativePrice += priceEvent.PriceChange;
            open ??= cumulativePrice;
            if (cumulativePrice > high) high = cumulativePrice;
            if (cumulativePrice < low) low = cumulativePrice;
            count++;
        }

        if (count == 0)
        {
            logger.LogInformation("No events for {AggregateId} on {Date}, skipping", aggregateId, dateTimeOffset);
            return;
        }

        var lastVersion = await eventStoreReader.GetLastVersionAsync(aggregateId, dateTimeOffset, ct) ?? 0;

        await writer.CreateHourlyReadModelAsync(
            new HourlyReadModelAggregate(aggregateId, dateTimeOffset, open.Value, low, high, cumulativePrice,
                cumulativePrice - open.Value, lastVersion),
            ct);

        logger.LogInformation("HourlyReadModelWorker finished for {AggregateId} on {Date}", aggregateId,
            dateTimeOffset);
    }
}