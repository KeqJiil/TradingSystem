using MediatR;

namespace Stock.Application.Queries.GetHourlyReadModel;

public class GetHourlyReadModelHandler() : IRequestHandler<GetHourlyReadModelQuery, HourlyReadModel>
{
    public async Task<HourlyReadModel> Handle(GetHourlyReadModelQuery request, CancellationToken ct)
    {
    }
}