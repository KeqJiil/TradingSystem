using MediatR;

namespace Stock.Application.Commands.RequestDailyReadModels;

public record RequestDailyReadModelsCommand(DateTime Date) : IRequest;
