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
        await producer.ProduceAsync(TopicNames.Stock,
            new Message<string, TEvent> { Value = @event, Key = @event.AggregateId.ToString() }, ct);
    }
}