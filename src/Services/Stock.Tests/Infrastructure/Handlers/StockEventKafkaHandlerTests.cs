using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Stock.Infrastructure.Handlers;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Options;
using Stock.Infrastructure.Serialization;
using Xunit;
using AppEvents = Stock.Application.Events;
using Ext = Stock.Infrastructure.ExternalEvents;

namespace Stock.Tests.Infrastructure.Handlers;

public class StockEventKafkaHandlerTests : IClassFixture<KafkaFixture>, IAsyncDisposable
{
    private readonly KafkaFixture _fixture;
    private readonly StockEventKafkaHandler _handler;
    private readonly IProducer<string, Ext.StockCreatedEvent> _stockCreatedProducer;
    private readonly IProducer<string, Ext.StockToggledStatusEvent> _stockToggledStatusProducer;
    private readonly IProducer<string, Ext.PriceChangedEvent> _priceChangedProducer;

    public StockEventKafkaHandlerTests(KafkaFixture fixture)
    {
        _fixture = fixture;

        var kafkaProducerFactory = new KafkaProducerFactory(Options.Create(new KafkaOptions
        {
            BootstrapServers = fixture.BootstrapAddress,
            ProducerClientId = "stock-event-handler-tests"
        }));

        _stockCreatedProducer = kafkaProducerFactory.Create<Ext.StockCreatedEvent>("stock-created-tests");
        _stockToggledStatusProducer =
            kafkaProducerFactory.Create<Ext.StockToggledStatusEvent>("stock-toggled-tests");
        _priceChangedProducer = kafkaProducerFactory.Create<Ext.PriceChangedEvent>("price-changed-tests");

        _handler = new StockEventKafkaHandler(_stockCreatedProducer, _stockToggledStatusProducer, _priceChangedProducer);
    }

    public ValueTask DisposeAsync()
    {
        _stockCreatedProducer.Dispose();
        _stockToggledStatusProducer.Dispose();
        _priceChangedProducer.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Handle_StockCreatedEvent_ProducesExternalEventToStockCreatedTopic()
    {
        var @event = new AppEvents.StockCreatedEvent(Guid.NewGuid(), "AAPL", true, "USD",
            new TimeOnly(9, 30), new TimeOnly(16, 0));

        await _handler.Handle(@event, CancellationToken.None);

        var result = Consume<Ext.StockCreatedEvent>(TopicNames.StockCreated);

        Assert.Equal(@event.AggregateId.ToString(), result.Message.Key);
        Assert.Equal(@event.AggregateId, result.Message.Value.AggregateId);
        Assert.Equal(@event.Name, result.Message.Value.Name);
        Assert.Equal(@event.IsOpenToTrade, result.Message.Value.IsOpenToTrade);
        Assert.Equal(@event.Currency, result.Message.Value.Currency);
        Assert.Equal(@event.TradingStartTime, result.Message.Value.TradingStartTime);
        Assert.Equal(@event.TradingCloseTime, result.Message.Value.TradingCloseTime);
    }

    [Fact]
    public async Task Handle_StockToggledStatusEvent_ProducesExternalEventToStockStatusToggledTopic()
    {
        var @event = new AppEvents.StockToggledStatusEvent(Guid.NewGuid(), DateTimeOffset.UtcNow);

        await _handler.Handle(@event, CancellationToken.None);

        var result = Consume<Ext.StockToggledStatusEvent>(TopicNames.StockStatusToggled);

        Assert.Equal(@event.AggregateId.ToString(), result.Message.Key);
        Assert.Equal(@event.AggregateId, result.Message.Value.AggregateId);
    }

    [Fact]
    public async Task Handle_PriceChangedEvent_ProducesExternalEventToPriceTopic()
    {
        var @event = new AppEvents.PriceChangedEvent(Guid.NewGuid(), 12.34m, 7, DateTimeOffset.UtcNow);

        await _handler.Handle(@event, CancellationToken.None);

        var result = Consume<Ext.PriceChangedEvent>(TopicNames.Price);

        Assert.Equal(@event.AggregateId.ToString(), result.Message.Key);
        Assert.Equal(@event.AggregateId, result.Message.Value.AggregateId);
        Assert.Equal(@event.PriceChange, result.Message.Value.PriceChange);
    }

    private ConsumeResult<string, TValue> Consume<TValue>(string topic)
    {
        using var consumer = new ConsumerBuilder<string, TValue>(new ConsumerConfig
            {
                BootstrapServers = _fixture.BootstrapAddress,
                GroupId = Guid.NewGuid().ToString(),
                AutoOffsetReset = AutoOffsetReset.Earliest
            })
            .SetValueDeserializer(new ProtobufNetDeserializer<TValue>())
            .Build();

        consumer.Subscribe(topic);
        var result = consumer.Consume(TimeSpan.FromSeconds(30));
        Assert.NotNull(result);
        consumer.Close();
        return result!;
    }
}
