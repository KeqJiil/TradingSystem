using Microsoft.Extensions.Options;
using Stock.Application.Commands.UpdateNameReadModel;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers.Dlq;

public class NameChangedDlqConsumer(
    IOptions<DeadLetterOptions> dlqOptions,
    ILogger<DlqRetryableConsumer<NameChangedEvent, UpdateNameReadModelCommand>> logger,
    IServiceScopeFactory serviceScopeFactory,
    IDlqEventToCommandMapper<NameChangedEvent, UpdateNameReadModelCommand> mapper,
    IKafkaConsumerFactory consumerFactory,
    IDeadLetterPublisher dlq)
    : DlqRetryableConsumer<NameChangedEvent, UpdateNameReadModelCommand>(dlqOptions, logger,
        serviceScopeFactory, mapper, consumerFactory, dlq)
{
    protected override string SourceTopic => TopicNames.StockNameChanged;
}
