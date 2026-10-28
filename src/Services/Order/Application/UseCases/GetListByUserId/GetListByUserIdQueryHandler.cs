using MediatR;
using Order.Application.Abstractions;

namespace Order.Application.UseCases.GetListByUserId;

public class GetListByUserIdQueryHandler : IRequestHandler<GetListByUserIdQuery, IReadOnlyList<OrderDto>>
{
    public Task<IReadOnlyList<OrderDto>> Handle(GetListByUserIdQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
