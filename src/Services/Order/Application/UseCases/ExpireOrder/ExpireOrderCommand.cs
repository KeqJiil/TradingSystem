using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.ExpireOrder;

public record ExpireOrderCommand(Guid OrderId) : IRequest<Result>;
