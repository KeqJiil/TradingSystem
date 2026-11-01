using MediatR;
using Order.Application.Abstractions;
using Order.Domain;

namespace Order.Application.UseCases.GetListByUserId;

public class GetListByUserIdQueryHandler(IOrderReader reader)
    : IRequestHandler<GetListByUserIdQuery, Result<IReadOnlyList<OrderDto>>>
{
    public async Task<Result<IReadOnlyList<OrderDto>>> Handle(GetListByUserIdQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Limit is < 1 or > 100 || (request.Status is not null && !Enum.IsDefined(request.Status.Value)) ||
            request.AfterCreatedAt.HasValue != request.AfterId.HasValue)
            return Result<IReadOnlyList<OrderDto>>.Failure(new Error
                { Type = ErrorType.Validation, Message = "Invalid request parameters" });

        var result = await reader.GetOrdersByUserIdAsync(request.UserId, request.Limit, request.Status,
            request.AfterCreatedAt, request.AfterId, cancellationToken);

        return Result<IReadOnlyList<OrderDto>>.Success(result);
    }
}