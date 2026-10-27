using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.RejectCancel;

public record RejectCancelCommand(Guid OrderId, string Reason) : IRequest<Result>;
