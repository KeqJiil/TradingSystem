using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Queries.GetHourlyReadModel;

namespace Stock.Application.Queries.GetHourReadModel;

public class GetHourReadModelHandler(IStockPriceHourlyReader reader)
    : IRequestHandler<GetHourReadModelQuery, PriceHourReadModel?>
{
    public Task<PriceHourReadModel?> Handle(GetHourReadModelQuery request, CancellationToken ct)
    {
        return reader.GetHourPriceHistoryAsync(request.AggregateId, request.DateTime, ct);
    }
}