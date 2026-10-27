using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.ApplyFill;

public class ApplyFillCommandHandler : IRequestHandler<ApplyFillCommand, Result>
{
    public Task<Result> Handle(ApplyFillCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
