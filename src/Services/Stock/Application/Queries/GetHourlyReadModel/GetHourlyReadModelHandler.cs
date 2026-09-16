using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Queries.GetHourlyReadModel;

public class GetHourlyReadModelHandler(IStockPriceHourlyReader reader)
    : IRequestHandler<GetHourlyReadModelQuery, HourlyReadModel?>
{
    public async Task<HourlyReadModel?> Handle(GetHourlyReadModelQuery request, CancellationToken ct)
    {
        var priceChanges = await reader.GetHourlyPriceHistoryAsync(request.AggregateId, request.From, request.To, ct);
        return new HourlyReadModel(request.AggregateId, priceChanges);
    }
}