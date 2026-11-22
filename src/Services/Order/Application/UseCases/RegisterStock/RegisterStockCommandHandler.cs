using MediatR;
using Order.Application.Abstractions;
using Order.Domain;
using Order.Domain.Aggregates;

namespace Order.Application.UseCases.RegisterStock;

public class RegisterStockCommandHandler(IStockRepository repository, IUnitOfWork uow)
    : IRequestHandler<RegisterStockCommand, Result>
{
    public async Task<Result> Handle(RegisterStockCommand request, CancellationToken cancellationToken)
    {
        if (await repository.GetByIdAsync(request.StockId, cancellationToken) is not null)
            return Result.Success();

        var stock = Stock.Create(request.StockId, request.Name, request.IsOpenToTrade, request.TradingStart,
            request.TradingEnd);
        if (stock.IsFailure)
            return stock.Error;

        repository.Add(stock.Value);
        await uow.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
