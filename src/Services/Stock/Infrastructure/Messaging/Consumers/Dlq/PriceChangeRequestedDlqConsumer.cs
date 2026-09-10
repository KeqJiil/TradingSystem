using Microsoft.Extensions.Options;
using Stock.Application.Abstractions;
using Stock.Application.Commands.ChangePrice;
using Stock.Application.Events;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers.Dlq;

public class PriceChangeRequestedDlqConsumer(
    IOptions<DeadLetterOptions> dlqOptions,
    ILogger<DlqRetryableConsumer<PriceChangeRequested, ChangePriceCommand>> logger,
    IServiceScopeFactory serviceScopeFactory,
    IDlqEventToCommandMapper<PriceChangeRequested, ChangePriceCommand> mapper,
    IKafkaConsumerFactory consumerFactory,
    IDeadLetterPublisher dlq)
    : DlqRetryableConsumer<PriceChangeRequested, ChangePriceCommand>(dlqOptions, logger, serviceScopeFactory, mapper,
        consumerFactory, dlq)
{
    protected override string SourceTopic => TopicNames.PriceChangeRequested;
}
