using Stock.Application.Commands.UpdateTradingTimeReadModel;
using Stock.Infrastructure.ExternalEvents;

namespace Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;

public class TimeChangedDlqEventToCommandMapper
    : IDlqEventToCommandMapper<TimeChangedEvent, UpdateTradingTimeCommand>
{
    public UpdateTradingTimeCommand Map(TimeChangedEvent message)
    {
        return new UpdateTradingTimeCommand(message.AggregateId, message.TradingStartTime,
            message.TradingCloseTime, message.Version);
    }
}
