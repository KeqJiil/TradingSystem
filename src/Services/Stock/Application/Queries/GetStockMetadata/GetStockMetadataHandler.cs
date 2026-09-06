using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Queries.GetStockMetadata;

public class GetStockMetadataHandler(IStockDataReader stockDataReader)
    : IRequestHandler<GetStockMetadataQuery, StockMetadata?>
{
    public Task<StockMetadata?> Handle(GetStockMetadataQuery request, CancellationToken cancellationToken)
    {
        return stockDataReader.GetByIdAsync(request.Id, cancellationToken);
    }
}
