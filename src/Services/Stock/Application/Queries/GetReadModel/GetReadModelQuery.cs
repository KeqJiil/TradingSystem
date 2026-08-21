using MediatR;
using Stock.Application.Abstractions;

namespace Stock.Application.Queries.GetReadModel;

public record GetReadModelQuery(Guid Id) : IRequest<StockReadModel>;