using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Queries.GetHourlyReadModel;

public class GetHourlyReadModelHandler(IStockPriceHistoryReader reader) : IRequestHandler<GetHourlyReadModelQuery, HourlyReadModel?>
{
    public Task<HourlyReadModel?> Handle(GetHourlyReadModelQuery request, CancellationToken ct)
    {
        return reader.GetHourlyPriceHistoryAsync(request.AggregateId, request.From, request.To, ct);
    }
}