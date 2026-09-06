using Confluent.Kafka;
using MediatR;
using Stock.Application.Events;

namespace Stock.Infrastructure.Handlers;

public class StockEventKafkaHandler<TEvent>(IProducer<string, TEvent> producer)
    : INotificationHandler<TEvent> where TEvent : StockEvent
{
    public async Task Handle(TEvent @event, CancellationToken ct)
    {
        await producer.ProduceAsync("stock.events",
            new Message<string, TEvent> { Value = @event, Key = @event.AggregateId.ToString() }, ct);
    }
}