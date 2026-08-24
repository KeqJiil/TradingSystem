using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Queries.GetWeeklyReadModel;

public class GetWeeklyReadModelHandler(IStockPriceHistoryReader reader) : IRequestHandler<GetWeeklyReadModelQuery, WeeklyReadModel>
{
    public async Task<WeeklyReadModel> Handle(GetWeeklyReadModelQuery request, CancellationToken cancellationToken)
    {
        var dailyReadModels = await reader.GetDailyPriceHistoryAsync(request.AggregateId, request.StartDate, request.EndDate, cancellationToken);

        return new WeeklyReadModel(request.AggregateId, dailyReadModels.ToList());
    }
}