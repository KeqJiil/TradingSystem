using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Stock.Application.Commands.CreateReadModel;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Messaging.Consumers.Dlq;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers.Dlq;

public class StockCreatedDlqConsumerTests
{
    [Fact]
    public void SourceTopic_IsStockCreatedTopic()
    {
        var sut = new StockCreatedDlqConsumer(
            Options.Create(new DeadLetterOptions()),
            NullLogger<DlqRetryableConsumer<StockCreatedEvent, CreateReadModelCommand>>.Instance,
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<IDlqEventToCommandMapper<StockCreatedEvent, CreateReadModelCommand>>(),
            Mock.Of<IKafkaConsumerFactory>(),
            Mock.Of<IDeadLetterPublisher>());

        var sourceTopic = typeof(DlqRetryableConsumer<StockCreatedEvent, CreateReadModelCommand>)
            .GetProperty("SourceTopic", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(sut);

        Assert.Equal(TopicNames.StockCreated, sourceTopic);
    }
}