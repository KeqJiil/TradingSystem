using Confluent.Kafka;
using MediatR;
using Stock.Application.Events;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Handlers;

public class StockEventKafkaHandler<TEvent>(IProducer<string, TEvent> producer)
    : INotificationHandler<TEvent> where TEvent : StockEvent
{
    public async Task Handle(TEvent @event, CancellationToken ct)
    {
        var topic = StockEventTopics.Resolve<TEvent>();
        await producer.ProduceAsync(topic,
            new Message<string, TEvent> { Value = @event, Key = @event.AggregateId.ToString() }, ct);
    }
}

public static class StockEventTopics
{
    public static string Resolve<TEvent>() where TEvent : StockEvent
    {
        return typeof(TEvent) switch
        {
            var t when t == typeof(StockCreatedEvent) => TopicNames.StockCreated,
            var t when t == typeof(StockToggledStatusEvent) => TopicNames.StockStatusToggled,
            var t when t == typeof(PriceChangedEvent) => TopicNames.Price,
            _ => throw new InvalidOperationException($"No Kafka topic mapped for event type {typeof(TEvent).Name}")
        };
    }
}