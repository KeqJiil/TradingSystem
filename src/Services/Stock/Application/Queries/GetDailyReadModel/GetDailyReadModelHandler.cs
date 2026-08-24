using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Queries.GetDailyReadModel;

public class GetDailyReadModelHandler(IStockPriceHistoryReader reader) : IRequestHandler<GetDailyReadModelQuery, DailyReadModel?>
{
    public async Task<DailyReadModel?> Handle(GetDailyReadModelQuery request, CancellationToken cancellationToken)
    {
        var dailyReadModel = await reader.GetDayPriceHistoryAsync(request.AggregateId, request.Date, cancellationToken);
        if (!dailyReadModel.HasValue) return null;
        
        return new DailyReadModel(
            request.AggregateId,
            request.Date,
            dailyReadModel.Value.OpenPrice,
            dailyReadModel.Value.ClosePrice,
            dailyReadModel.Value.HighPrice,
            dailyReadModel.Value.LowPrice,
            dailyReadModel.Value.Difference
        );
    }
}