using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.RequestCancel;

public record RequestCancelCommand(Guid OrderId) : IRequest<Result>;
