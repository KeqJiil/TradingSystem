using MediatR;
using Messaging.Abstractions;
using Order.Application.UseCases.ApproveOrder;
using Order.Infrastructure.Messaging.Reports;

namespace Order.Infrastructure.Messaging.Consumers;

public class OrderAcceptedConsumer(IMediator mediator) : IMessageConsumer<OrderAcceptedReport>
{
    public async Task<ConsumeOutcome> ConsumeAsync(OrderAcceptedReport message, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ApproveOrderCommand(message.OrderId), cancellationToken);
        return result.ToConsumeOutcome();
    }
}
