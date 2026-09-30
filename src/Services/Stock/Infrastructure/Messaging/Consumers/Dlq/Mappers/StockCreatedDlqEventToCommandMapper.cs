using Stock.Application.Commands.CreateReadModel;
using Stock.Infrastructure.ExternalEvents;

namespace Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;

public class StockCreatedDlqEventToCommandMapper : IDlqEventToCommandMapper<StockCreatedEvent, CreateReadModelCommand>
{
    public CreateReadModelCommand Map(StockCreatedEvent message)
    {
        return new CreateReadModelCommand(message.AggregateId, message.Name, message.IsOpenToTrade,
            message.Currency, message.TradingStartTime, message.TradingCloseTime);
    }
}
