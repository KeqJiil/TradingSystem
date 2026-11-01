using MediatR;
using Messaging.Abstractions;
using Order.Application.UseCases.ExpireOrder;
using Order.Infrastructure.Messaging.Reports;

namespace Order.Infrastructure.Messaging.Consumers;

public class OrderExpiredConsumer(IMediator mediator) : IMessageConsumer<OrderExpiredReport>
{
    public async Task<ConsumeOutcome> ConsumeAsync(OrderExpiredReport message, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ExpireOrderCommand(message.OrderId, message.ExpiredAt), cancellationToken);
        return result.ToConsumeOutcome();
    }
}
