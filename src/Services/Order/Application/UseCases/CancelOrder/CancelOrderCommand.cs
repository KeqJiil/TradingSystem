using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.CancelOrder;

public record CancelOrderCommand(Guid OrderId, string Reason) : IRequest<Result>;
