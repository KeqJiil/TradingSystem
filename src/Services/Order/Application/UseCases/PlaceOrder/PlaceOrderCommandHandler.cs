using MediatR;
using Order.Application.Abstractions;
using Order.Domain;
using Order.Domain.VO;
using OrderAggregate = Order.Domain.Aggregates.Order;

namespace Order.Application.UseCases.PlaceOrder;

public class PlaceOrderCommandHandler(IOrderRepository repository, IStockRepository stockRepository, IUnitOfWork unitOfWork, TimeProvider timeProvider) : IRequestHandler<PlaceOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var limitPrice = Price.CreateOptional(request.LimitPrice);
        if (limitPrice.IsFailure)
            return limitPrice.Error;

        var orderTerms = OrderTerms.Create(request.TimeInForce, request.Type, limitPrice.Value);
        if (orderTerms.IsFailure)
            return orderTerms.Error;

        var quantity = Quantity.Create(request.Quantity);
        if (quantity.IsFailure)
            return quantity.Error;

        var stock = await stockRepository.GetByIdAsync(request.StockId, cancellationToken);
        if (stock is null)
            return new Error { Message = $"Stock with id {request.StockId} not found", Type = ErrorType.NotFound };

        var order = OrderAggregate.Create(request.OrderId, request.UserId, stock, quantity.Value, orderTerms.Value, request.Side, timeProvider.GetUtcNow());
        if (order.IsFailure)
            return order.Error;

        repository.Add(order.Value);

        await unitOfWork.CommitAsync(cancellationToken);

        return order.Value.Id;
    }
}
