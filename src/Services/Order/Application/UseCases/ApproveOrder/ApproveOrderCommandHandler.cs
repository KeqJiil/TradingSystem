using MediatR;
using Order.Application.Abstractions;
using Order.Domain;

namespace Order.Application.UseCases.ApproveOrder;

public class ApproveOrderCommandHandler(IOrderRepository repository, IUnitOfWork uow, TimeProvider timeProvider) : IRequestHandler<ApproveOrderCommand, Result>
{
    public async Task<Result> Handle(ApproveOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Failure(new Error { Type = ErrorType.NotFound, Message = $"Order with id {request.OrderId} not found" });

        var result = order.Approve(timeProvider.GetUtcNow());
        if (result.IsFailure)
            return result.Error;

        await uow.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
