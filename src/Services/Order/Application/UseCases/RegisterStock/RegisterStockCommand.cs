using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.RegisterStock;

public record RegisterStockCommand(
    Guid StockId,
    string Name,
    bool IsOpenToTrade,
    TimeOnly TradingStart,
    TimeOnly TradingEnd) : IRequest<Result>;
