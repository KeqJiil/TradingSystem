using MediatR;
using Messaging.Abstractions;
using Order.Application.UseCases.CancelOrder;
using Order.Infrastructure.Messaging.Reports;

namespace Order.Infrastructure.Messaging.Consumers;

public class OrderCanceledConsumer(IMediator mediator) : IMessageConsumer<OrderCanceledReport>
{
    public async Task<ConsumeOutcome> ConsumeAsync(OrderCanceledReport message, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CancelOrderCommand(message.OrderId, message.Reason, message.CanceledAt), cancellationToken);
        return result.ToConsumeOutcome();
    }
}
