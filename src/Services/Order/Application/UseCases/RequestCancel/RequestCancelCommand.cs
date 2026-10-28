using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.RequestCancel;

public record RequestCancelCommand(Guid OrderId, Guid UserId) : IRequest<Result>;