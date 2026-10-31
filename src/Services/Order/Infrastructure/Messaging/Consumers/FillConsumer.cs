using MediatR;
using Messaging.Abstractions;
using Order.Application.UseCases.ApplyFill;
using Order.Infrastructure.Messaging.Reports;

namespace Order.Infrastructure.Messaging.Consumers;

public class FillConsumer(IMediator mediator) : IMessageConsumer<FillReport>
{
    public async Task<ConsumeOutcome> ConsumeAsync(FillReport message, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ApplyFillCommand(message.OrderId, message.ExecutionId, message.Quantity, message.Price, message.FilledAt), cancellationToken);
        return result.ToConsumeOutcome();
    }
}
