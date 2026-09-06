using MediatR;
using Microsoft.AspNetCore.Mvc;
using Stock.Application.Queries.GetStockMetadata;

namespace Stock.Presentation.Http.Controllers;

public static class StockMetadataController
{
    public static void MapStockMetadataController(this WebApplication app)
    {
        app.MapGet("/api/stock/{id:Guid}/metadata", async (
                [FromRoute] Guid id,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var metadata = await mediator.Send(new GetStockMetadataQuery(id), cancellationToken);
                return metadata is not null ? Results.Ok(metadata) : Results.NotFound();
            })
            .WithName("GetStockMetadata")
            .WithTags("Stock")
            .WithDescription("Gets a stock's metadata")
            .Produces(200)
            .Produces(404);
    }
}
