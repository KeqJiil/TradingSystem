using MediatR;
using Messaging.Abstractions;
using Order.Application.UseCases.RejectCancel;
using Order.Infrastructure.Messaging.Reports;

namespace Order.Infrastructure.Messaging.Consumers;

public class CancelRejectedConsumer(IMediator mediator) : IMessageConsumer<CancelRejectedReport>
{
    public async Task<ConsumeOutcome> ConsumeAsync(CancelRejectedReport message, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RejectCancelCommand(message.OrderId, message.Reason, message.RejectedAt), cancellationToken);
        return result.ToConsumeOutcome();
    }
}
