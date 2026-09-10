using Microsoft.Extensions.Options;
using Stock.Application.Abstractions;
using Stock.Application.Commands.CreateReadModel;
using Stock.Application.Events;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers.Dlq;

public class StockCreatedDlqConsumer(
    IOptions<DeadLetterOptions> dlqOptions,
    ILogger<DlqRetryableConsumer<StockCreatedEvent, CreateReadModelCommand>> logger,
    IServiceScopeFactory serviceScopeFactory,
    IDlqEventToCommandMapper<StockCreatedEvent, CreateReadModelCommand> mapper,
    IKafkaConsumerFactory consumerFactory,
    IDeadLetterPublisher dlq)
    : DlqRetryableConsumer<StockCreatedEvent, CreateReadModelCommand>(dlqOptions, logger, serviceScopeFactory, mapper,
        consumerFactory, dlq)
{
    protected override string SourceTopic => TopicNames.StockCreated;
}
