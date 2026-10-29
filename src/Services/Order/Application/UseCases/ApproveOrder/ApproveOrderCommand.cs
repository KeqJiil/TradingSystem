using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.ApproveOrder;

public record ApproveOrderCommand(Guid OrderId) : IRequest<Result>;
