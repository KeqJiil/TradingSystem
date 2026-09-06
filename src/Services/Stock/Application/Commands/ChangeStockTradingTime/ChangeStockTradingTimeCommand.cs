using MediatR;

namespace Stock.Application.Commands.ChangeStockTradingTime;

public record ChangeStockTradingTimeCommand(Guid Id, TimeOnly OpenTime, TimeOnly CloseTime) : IRequest;
