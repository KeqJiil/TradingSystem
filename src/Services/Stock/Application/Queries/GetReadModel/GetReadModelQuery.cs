using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Queries.GetReadModel;

public record struct GetReadModelQuery(Guid Id) : IRequest<StockReadModel>;