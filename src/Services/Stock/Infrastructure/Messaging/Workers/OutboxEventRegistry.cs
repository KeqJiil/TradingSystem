using Ap = Stock.Application.Events;
using Ex = Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;
using Stock.Infrastructure.Serialization;

namespace Stock.Infrastructure.Messaging.Workers;

public static class OutboxEventRegistry
{
    private static readonly IReadOnlyDictionary<string, OutboxEventDescriptor> Entries =
        new Dictionary<string, OutboxEventDescriptor>
        {
            [EventTypeNames.PriceChanged] = new(
                payload => payload.TryDeserialize<Ap.PriceChangedEvent>(),
                (dlq, e, attempt, ct) => dlq.PublishAsync(TopicNames.Price,
                    Ex.PriceChangedEventMapper.MapToExternal((Ap.PriceChangedEvent)e), false, attempt, ct)),

            [EventTypeNames.StockCreated] = new(
                payload => payload.TryDeserialize<Ap.StockCreatedEvent>(),
                (dlq, e, attempt, ct) => dlq.PublishAsync(TopicNames.StockCreated,
                    Ex.StockCreatedEventMapper.MapToExternal((Ap.StockCreatedEvent)e), false, attempt, ct)),

            [EventTypeNames.StockToggledStatus] = new(
                payload => payload.TryDeserialize<Ap.StockToggledStatusEvent>(),
                (dlq, e, attempt, ct) => dlq.PublishAsync(TopicNames.StockStatusToggled,
                    Ex.StockToggledStatusEventMapper.MapToExternal((Ap.StockToggledStatusEvent)e), false, attempt, ct)),

            [EventTypeNames.NameChanged] = new(
                payload => payload.TryDeserialize<Ap.NameChangedEvent>(),
                (dlq, e, attempt, ct) => dlq.PublishAsync(TopicNames.StockNameChanged,
                    Ex.NameChangedEventMapper.MapToExternal((Ap.NameChangedEvent)e), false, attempt, ct)),

            [EventTypeNames.TimeChanged] = new(
                payload => payload.TryDeserialize<Ap.TimeChangedEvent>(),
                (dlq, e, attempt, ct) => dlq.PublishAsync(TopicNames.StockTradingTimeChanged,
                    Ex.TimeChangedEventMapper.MapToExternal((Ap.TimeChangedEvent)e), false, attempt, ct)),

            [EventTypeNames.DailyReadModelRequested] = new(
                payload => payload.TryDeserialize<Ap.DailyReadModelRequested>(),
                null,
                IsJob: true),

            [EventTypeNames.HourlyReadModelRequested] = new(
                payload => payload.TryDeserialize<Ap.HourlyReadModelRequested>(),
                null,
                IsJob: true)
        };

    public static bool TryGet(string eventType, out OutboxEventDescriptor descriptor)
    {
        return Entries.TryGetValue(eventType, out descriptor!);
    }
}

public sealed record OutboxEventDescriptor(
    Func<string, Ap.BasicEvent?> Deserialize,
    Func<IDeadLetterPublisher, Ap.BasicEvent, int, CancellationToken, Task>? PublishToOwnTopic,
    bool IsJob = false);