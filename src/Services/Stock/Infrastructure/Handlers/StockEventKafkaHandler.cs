using Confluent.Kafka;
using MediatR;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Observability;
using Stock.Infrastructure.Options;
using Stock.Infrastructure.Serialization;

namespace Stock.Infrastructure.Handlers;

public class StockEventKafkaHandler(
    IKafkaPublisher producer)
    : INotificationHandler<Application.Events.StockCreatedEvent>,
        INotificationHandler<Application.Events.StockToggledStatusEvent>,
        INotificationHandler<Application.Events.PriceChangedEvent>,
        INotificationHandler<Application.Events.NameChangedEvent>,
        INotificationHandler<Application.Events.TimeChangedEvent>
{
    public Task Handle(Application.Events.StockCreatedEvent @event, CancellationToken ct)
    {
        return Produce(producer, TopicNames.StockCreated, @event.AggregateId,
            StockCreatedEventMapper.MapToExternal(@event), ct);
    }

    public Task Handle(Application.Events.StockToggledStatusEvent @event, CancellationToken ct)
    {
        return Produce(producer, TopicNames.StockStatusToggled, @event.AggregateId,
            StockToggledStatusEventMapper.MapToExternal(@event), ct);
    }

    public Task Handle(Application.Events.PriceChangedEvent @event, CancellationToken ct)
    {
        return Produce(producer, TopicNames.Price, @event.AggregateId,
            PriceChangedEventMapper.MapToExternal(@event), ct);
    }

    public Task Handle(Application.Events.NameChangedEvent @event, CancellationToken ct)
    {
        return Produce(producer, TopicNames.StockNameChanged, @event.AggregateId,
            NameChangedEventMapper.MapToExternal(@event), ct);
    }

    public Task Handle(Application.Events.TimeChangedEvent @event, CancellationToken ct)
    {
        return Produce(producer, TopicNames.StockTradingTimeChanged, @event.AggregateId,
            TimeChangedEventMapper.MapToExternal(@event), ct);
    }

    private static Task Produce<TExternal>(IKafkaPublisher producer, string topic, Guid key,
        TExternal value, CancellationToken ct) where TExternal : IExternalEvent
    {
        var message = new Message<string, byte[]>
        {
            Key = key.ToString(),
            Value = new ProtobufNetSerializer<TExternal>().Serialize(value,
                new SerializationContext(MessageComponentType.Value, topic))
        };

        return producer.PublishAsync(topic, message, ct);
    }
}