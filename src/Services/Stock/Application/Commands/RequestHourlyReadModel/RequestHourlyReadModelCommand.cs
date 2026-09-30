using MediatR;

namespace Stock.Application.Commands.RequestHourlyReadModel;

public record struct RequestHourlyReadModelCommand(
    DateOnly Date,
    byte Hour) : IRequest;