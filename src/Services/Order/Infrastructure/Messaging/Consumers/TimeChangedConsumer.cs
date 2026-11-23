using MediatR;
using Messaging.Abstractions;
using Order.Application.UseCases.ChangeStockTradingTime;
using Order.Infrastructure.Messaging.Serialization;
using TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Order.Infrastructure.Messaging.Consumers;

public class TimeChangedConsumer(IMediator mediator) : IMessageConsumer<TimeChangedEvent>
{
    public async Task<ConsumeOutcome> ConsumeAsync(TimeChangedEvent message, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message.AggregateId, out var stockId))
            return new ConsumeOutcome(MessageConsumeResult.DeadLetter, "Invalid stock id.");

        if (!message.TradingStartTime.TryToTimeOnly(out var start) ||
            !message.TradingCloseTime.TryToTimeOnly(out var end))
            return new ConsumeOutcome(MessageConsumeResult.DeadLetter, "Invalid trading time.");

        var result = await mediator.Send(new ChangeStockTradingTimeCommand(stockId, start, end, message.Version),
            cancellationToken);
        return result.ToConsumeOutcome();
    }
}
