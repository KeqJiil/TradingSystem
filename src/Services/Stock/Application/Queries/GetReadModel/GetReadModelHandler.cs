using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Queries.GetReadModel;

public class GetReadModelHandler : IRequestHandler<GetReadModelQuery, StockReadModel>
{
    private readonly IStockReader _stockReader;

    public GetReadModelHandler(IStockReader stockReader)
    {
        _stockReader = stockReader;
    }

    public async Task<StockReadModel> Handle(GetReadModelQuery request, CancellationToken cancellationToken)
    {
        var readModel = await _stockReader.GetByIdAsync(request.Id, cancellationToken);
        return readModel;
    }
}