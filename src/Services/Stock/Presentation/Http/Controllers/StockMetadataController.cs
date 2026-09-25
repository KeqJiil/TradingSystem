using MediatR;
using Microsoft.AspNetCore.Mvc;
using Stock.Application.Abstractions;
using Stock.Application.Queries.GetStockMetadata;

namespace Stock.Presentation.Http.Controllers;

public static class StockMetadataController
{
    public static void MapStockMetadataController(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id:Guid}/metadata", async (
                [FromRoute] Guid id,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var metadata = await mediator.Send(new GetStockMetadataQuery(id), cancellationToken);
                return metadata is not null ? Results.Ok(metadata) : Results.NotFound();
            })
            .WithName("GetStockMetadata")
            .WithDescription("Gets a stock's metadata")
            .Produces<StockMetadata>(200)
            .ProducesProblem(404);
    }
}
