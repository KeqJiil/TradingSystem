using Microsoft.Extensions.Options;
using Stock.Application.Commands.UpdateTradingTimeReadModel;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers.Dlq;

public class TimeChangedDlqConsumer(
    IOptions<DeadLetterOptions> dlqOptions,
    ILogger<DlqRetryableConsumer<TimeChangedEvent, UpdateTradingTimeCommand>> logger,
    IServiceScopeFactory serviceScopeFactory,
    IDlqEventToCommandMapper<TimeChangedEvent, UpdateTradingTimeCommand> mapper,
    IKafkaConsumerFactory consumerFactory,
    IDeadLetterPublisher dlq)
    : DlqRetryableConsumer<TimeChangedEvent, UpdateTradingTimeCommand>(dlqOptions, logger,
        serviceScopeFactory, mapper, consumerFactory, dlq)
{
    protected override string SourceTopic => TopicNames.StockTradingTimeChanged;
}
