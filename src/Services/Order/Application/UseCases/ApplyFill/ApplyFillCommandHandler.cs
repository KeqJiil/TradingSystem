using MediatR;
using Order.Application.Abstractions;
using Order.Domain;
using Order.Domain.VO;

namespace Order.Application.UseCases.ApplyFill;

public class ApplyFillCommandHandler(IOrderRepository repository, IUnitOfWork uow) : IRequestHandler<ApplyFillCommand, Result>
{
    public async Task<Result> Handle(ApplyFillCommand request, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Failure(new Error { Type = ErrorType.NotFound, Message = $"Order with id {request.OrderId} not found" });
        
        var quantity = Quantity.Create(request.Quantity);
        if (quantity.IsFailure)
            return quantity.Error;
        
        var price = Price.Create(request.Price);
        if (price.IsFailure)
            return price.Error;
        
        var result = order.ApplyFill(request.ExecutionId, quantity.Value, price.Value, request.FilledAt);
        if (result.IsFailure)
            return result.Error;
        
        await uow.CommitAsync(cancellationToken);
        
        return Result.Success();
    }
}
