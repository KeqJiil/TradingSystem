using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.RejectOrder;

public record RejectOrderCommand(Guid OrderId, string Reason) : IRequest<Result>;
