using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.RejectCancel;

public class RejectCancelCommandHandler : IRequestHandler<RejectCancelCommand, Result>
{
    public Task<Result> Handle(RejectCancelCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
