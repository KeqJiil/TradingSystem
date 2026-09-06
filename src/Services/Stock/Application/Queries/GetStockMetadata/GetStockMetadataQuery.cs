using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Queries.GetStockMetadata;

public record struct GetStockMetadataQuery(Guid Id) : IRequest<StockMetadata?>;
