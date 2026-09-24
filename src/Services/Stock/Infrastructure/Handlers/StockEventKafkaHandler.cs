using Confluent.Kafka;
using MediatR;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Observability;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Handlers;

public class StockEventKafkaHandler(
    IProducer<string, StockCreatedEvent> stockCreatedProducer,
    IProducer<string, StockToggledStatusEvent> stockToggledStatusProducer,
    IProducer<string, PriceChangedEvent> priceChangedProducer,
    IProducer<string, NameChangedEvent> nameChangedProducer,
    IProducer<string, TimeChangedEvent> timeChangedProducer)
    : INotificationHandler<Application.Events.StockCreatedEvent>,
        INotificationHandler<Application.Events.StockToggledStatusEvent>,
        INotificationHandler<Application.Events.PriceChangedEvent>,
        INotificationHandler<Application.Events.NameChangedEvent>,
        INotificationHandler<Application.Events.TimeChangedEvent>
{
    public Task Handle(Application.Events.StockCreatedEvent @event, CancellationToken ct)
    {
        return Produce(stockCreatedProducer, TopicNames.StockCreated, @event.AggregateId,
            StockCreatedEventMapper.MapToExternal(@event), ct);
    }

    public Task Handle(Application.Events.StockToggledStatusEvent @event, CancellationToken ct)
    {
        return Produce(stockToggledStatusProducer, TopicNames.StockStatusToggled, @event.AggregateId,
            StockToggledStatusEventMapper.MapToExternal(@event), ct);
    }

    public Task Handle(Application.Events.PriceChangedEvent @event, CancellationToken ct)
    {
        return Produce(priceChangedProducer, TopicNames.Price, @event.AggregateId,
            PriceChangedEventMapper.MapToExternal(@event), ct);
    }

    public Task Handle(Application.Events.NameChangedEvent @event, CancellationToken ct)
    {
        return Produce(nameChangedProducer, TopicNames.StockNameChanged, @event.AggregateId,
            NameChangedEventMapper.MapToExternal(@event), ct);
    }

    public Task Handle(Application.Events.TimeChangedEvent @event, CancellationToken ct)
    {
        return Produce(timeChangedProducer, TopicNames.StockTradingTimeChanged, @event.AggregateId,
            TimeChangedEventMapper.MapToExternal(@event), ct);
    }

    private static Task Produce<TExternal>(IProducer<string, TExternal> producer, string topic, Guid key,
        TExternal value, CancellationToken ct)
    {
        return producer.ProduceTracedAsync(topic,
            new Message<string, TExternal> { Value = value, Key = key.ToString() }, ct);
    }
}