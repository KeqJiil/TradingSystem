using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.ChangeStockTradingTime;

public record ChangeStockTradingTimeCommand(Guid StockId, TimeOnly TradingStart, TimeOnly TradingEnd, long Version)
    : IRequest<Result>;
