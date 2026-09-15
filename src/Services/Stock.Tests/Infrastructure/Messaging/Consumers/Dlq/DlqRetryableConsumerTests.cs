using Confluent.Kafka;
using Confluent.Kafka.Admin;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stock.Application.Commands.CreateReadModel;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Messaging.Consumers.Dlq;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;
using Stock.Infrastructure.Serialization;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers.Dlq;

public class DlqRetryableConsumerTests : IClassFixture<KafkaFixture>
{
    private readonly KafkaFixture _fixture;
    private readonly KafkaProducerFactory _producerFactory;
    private readonly KafkaConsumerFactory _consumerFactory;

    public DlqRetryableConsumerTests(KafkaFixture fixture)
    {
        _fixture = fixture;
        var kafkaOptions = Options.Create(new KafkaOptions
        {
            BootstrapServers = fixture.BootstrapAddress,
            ProducerClientId = "dlq-retryable-tests"
        });
        _producerFactory = new KafkaProducerFactory(kafkaOptions);
        _consumerFactory = new KafkaConsumerFactory(kafkaOptions);
    }

    [Fact]
    public async Task Consume_HandlerSucceeds_CommitsAndDoesNotRepublishToDlq()
    {
        var sourceTopic = UniqueTopic();
        var handler = new StubHandler();

        await EnsureTopicsExistAsync(RetryTopic(sourceTopic), FatalTopic(sourceTopic));
        await using var harness = CreateHarness(sourceTopic, new StockCreatedDlqEventToCommandMapper(), handler);
        Seed(sourceTopic, NewStockCreatedEvent());

        await harness.Sut.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => handler.ReceivedCount >= 1);

        var retryMessages = ConsumeAll<StockCreatedEvent>(RetryTopic(sourceTopic));
        var fatalMessages = ConsumeAll<StockCreatedEvent>(FatalTopic(sourceTopic));

        Assert.Single(retryMessages);
        Assert.Empty(fatalMessages);
        Assert.Equal(1, handler.ReceivedCount);
    }

    [Fact]
    public async Task Consume_HandlerThrowsBelowMaxAttempts_RepublishesToRetryWithIncrementedAttempt()
    {
        var sourceTopic = UniqueTopic();
        var handler = new StubHandler { FailFirstNCalls = 1 };

        await EnsureTopicsExistAsync(RetryTopic(sourceTopic), FatalTopic(sourceTopic));
        await using var harness = CreateHarness(sourceTopic, new StockCreatedDlqEventToCommandMapper(), handler,
            5);
        Seed(sourceTopic, NewStockCreatedEvent(), 0);

        await harness.Sut.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => handler.ReceivedCount >= 2);

        var retryResults = ConsumeAllWithHeaders<StockCreatedEvent>(RetryTopic(sourceTopic));
        var fatalMessages = ConsumeAll<StockCreatedEvent>(FatalTopic(sourceTopic));

        Assert.Equal(2, retryResults.Count);
        Assert.Equal(1, GetAttempt(retryResults[1].Message.Headers));
        Assert.Empty(fatalMessages);
        Assert.Equal(2, handler.ReceivedCount);
    }

    [Fact]
    public async Task Consume_HandlerThrowsAtMaxAttempts_RoutesToFatalInsteadOfRetry()
    {
        var sourceTopic = UniqueTopic();
        var handler = new StubHandler { Fail = true };

        await EnsureTopicsExistAsync(RetryTopic(sourceTopic), FatalTopic(sourceTopic));
        await using var harness = CreateHarness(sourceTopic, new StockCreatedDlqEventToCommandMapper(), handler,
            1);
        Seed(sourceTopic, NewStockCreatedEvent(), 0);

        await harness.Sut.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => ConsumeAll<StockCreatedEvent>(FatalTopic(sourceTopic)).Count >= 1);

        var retryMessages = ConsumeAll<StockCreatedEvent>(RetryTopic(sourceTopic));
        var fatalMessages = ConsumeAll<StockCreatedEvent>(FatalTopic(sourceTopic));

        Assert.Single(retryMessages);
        Assert.Single(fatalMessages);
    }

    [Fact]
    public async Task Consume_MapperReturnsNull_GoesStraightToFatalWithoutCallingMediator()
    {
        var sourceTopic = UniqueTopic();
        var handler = new StubHandler();

        await EnsureTopicsExistAsync(RetryTopic(sourceTopic), FatalTopic(sourceTopic));
        await using var harness = CreateHarness(sourceTopic, new NullMapper(), handler);
        Seed(sourceTopic, NewStockCreatedEvent());

        await harness.Sut.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => ConsumeAll<StockCreatedEvent>(FatalTopic(sourceTopic)).Count >= 1);

        var fatalMessages = ConsumeAll<StockCreatedEvent>(FatalTopic(sourceTopic));

        Assert.Single(fatalMessages);
        Assert.Equal(0, handler.ReceivedCount);
    }

    private TestHarness CreateHarness(
        string sourceTopic,
        IDlqEventToCommandMapper<StockCreatedEvent, CreateReadModelCommand> mapper,
        StubHandler handler,
        int maxRetryAttempts = 5)
    {
        var dlqOptions = Options.Create(new DeadLetterOptions { MaxRetryAttempts = maxRetryAttempts });
        var dlq = new DeadLetterPublisher(_producerFactory, dlqOptions);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(IMediator).Assembly));
        services.AddScoped<IRequestHandler<CreateReadModelCommand>>(_ => handler);
        var provider = services.BuildServiceProvider();

        var sut = new TestableDlqConsumer(
            dlqOptions,
            new ConsoleLogger<DlqRetryableConsumer<StockCreatedEvent, CreateReadModelCommand>>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            mapper,
            _consumerFactory,
            dlq,
            sourceTopic);

        return new TestHarness(sut, provider);
    }

    private sealed class ConsoleLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
            if (exception is not null)
                Console.WriteLine(exception);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private async Task EnsureTopicsExistAsync(params string[] topics)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = _fixture.BootstrapAddress
            })
            .Build();

        try
        {
            await admin.CreateTopicsAsync(topics.Select(t => new TopicSpecification
            {
                Name = t,
                NumPartitions = 1,
                ReplicationFactor = 1
            }));
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
        }
    }

    private void Seed(string sourceTopic, StockCreatedEvent @event, int? attempt = null)
    {
        using var producer = _producerFactory.Create<StockCreatedEvent>($"seed-{Guid.NewGuid()}");
        var headers = new Headers();
        if (attempt is not null)
            headers.Add("attempt-count", BitConverter.GetBytes(attempt.Value));

        producer.Produce(RetryTopic(sourceTopic),
            new Message<string, StockCreatedEvent>
                { Key = @event.AggregateId.ToString(), Value = @event, Headers = headers });
        producer.Flush(TimeSpan.FromSeconds(10));
    }

    private static StockCreatedEvent NewStockCreatedEvent()
    {
        return new StockCreatedEvent(Guid.NewGuid(), "AAPL", true, "USD", new TimeOnly(9, 30), new TimeOnly(16, 0));
    }

    private static string UniqueTopic()
    {
        return $"stock-created-topic-{Guid.NewGuid()}";
    }

    private static string RetryTopic(string sourceTopic)
    {
        return sourceTopic + ".dlq.retry";
    }

    private static string FatalTopic(string sourceTopic)
    {
        return sourceTopic + ".dlq.fatal";
    }

    private static int GetAttempt(Headers headers)
    {
        return headers.TryGetLastBytes("attempt-count", out var bytes) ? BitConverter.ToInt32(bytes) : 0;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutSeconds = 15)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(200);
    }

    private List<ConsumeResult<string, TValue>> ConsumeAllWithHeaders<TValue>(string topic)
    {
        using var consumer = new ConsumerBuilder<string, TValue>(new ConsumerConfig
            {
                BootstrapServers = _fixture.BootstrapAddress,
                GroupId = $"monitor-{Guid.NewGuid()}",
                AutoOffsetReset = AutoOffsetReset.Earliest
            })
            .SetValueDeserializer(new ProtobufNetDeserializer<TValue>())
            .Build();

        consumer.Subscribe(topic);
        var results = new List<ConsumeResult<string, TValue>>();
        while (true)
        {
            var result = consumer.Consume(TimeSpan.FromSeconds(2));
            if (result is null) break;
            results.Add(result);
        }

        consumer.Close();
        return results;
    }

    private List<TValue> ConsumeAll<TValue>(string topic)
    {
        return ConsumeAllWithHeaders<TValue>(topic).ConvertAll(r => r.Message.Value);
    }

    private sealed class TestHarness(TestableDlqConsumer sut, ServiceProvider provider) : IAsyncDisposable
    {
        public TestableDlqConsumer Sut { get; } = sut;

        public async ValueTask DisposeAsync()
        {
            await Sut.StopAsync(CancellationToken.None);
            Sut.Dispose();
            await provider.DisposeAsync();
        }
    }

    private sealed class TestableDlqConsumer(
        IOptions<DeadLetterOptions> dlqOptions,
        ILogger<DlqRetryableConsumer<StockCreatedEvent, CreateReadModelCommand>> logger,
        IServiceScopeFactory scopeFactory,
        IDlqEventToCommandMapper<StockCreatedEvent, CreateReadModelCommand> mapper,
        IKafkaConsumerFactory consumerFactory,
        IDeadLetterPublisher dlq,
        string sourceTopic)
        : DlqRetryableConsumer<StockCreatedEvent, CreateReadModelCommand>(dlqOptions, logger, scopeFactory, mapper,
            consumerFactory, dlq)
    {
        protected override string SourceTopic => sourceTopic;
    }

    private sealed class StubHandler : IRequestHandler<CreateReadModelCommand>
    {
        public bool Fail { get; init; }
        public int FailFirstNCalls { get; init; }
        public int ReceivedCount { get; private set; }

        public Task Handle(CreateReadModelCommand request, CancellationToken cancellationToken)
        {
            ReceivedCount++;
            if (Fail || ReceivedCount <= FailFirstNCalls)
                throw new InvalidOperationException("simulated handler failure");
            return Task.CompletedTask;
        }
    }

    private sealed class NullMapper : IDlqEventToCommandMapper<StockCreatedEvent, CreateReadModelCommand>
    {
        public CreateReadModelCommand? Map(StockCreatedEvent message)
        {
            return null;
        }
    }
}