using MediatR;

namespace Stock.Application.Commands.CreateDailyReadModel;

public record CreateDailyReadModelCommand(DateTime Date) : IRequest;