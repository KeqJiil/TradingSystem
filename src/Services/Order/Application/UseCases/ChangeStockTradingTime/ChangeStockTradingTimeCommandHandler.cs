using MediatR;
using Order.Application.Abstractions;
using Order.Domain;

namespace Order.Application.UseCases.ChangeStockTradingTime;

public class ChangeStockTradingTimeCommandHandler(IStockRepository repository, IUnitOfWork uow)
    : IRequestHandler<ChangeStockTradingTimeCommand, Result>
{
    public async Task<Result> Handle(ChangeStockTradingTimeCommand request, CancellationToken cancellationToken)
    {
        var stock = await repository.GetByIdAsync(request.StockId, cancellationToken);
        if (stock is null)
            return Result.Failure(new Error
                { Type = ErrorType.NotFound, Message = $"Stock with id {request.StockId} not found" });

        var result = stock.SetTradingTime(request.TradingStart, request.TradingEnd, request.Version);
        if (result.IsFailure)
            return result.Error;

        await uow.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
