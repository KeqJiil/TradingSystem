using MediatR;
using Microsoft.AspNetCore.Mvc;
using Stock.Application.Queries.GetDailyReadModel;
using Stock.Application.Queries.GetHourlyReadModel;
using Stock.Application.Queries.GetReadModel;
using Stock.Application.Queries.GetWeeklyReadModel;

namespace Stock.Presentation.Http.Controllers;

public static class StockReadController
{
    public static void MapStockReadController(this WebApplication app)
    {
        app.MapGet("/api/stock/{id:Guid}", async (
                [FromRoute] Guid id,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var readModel = await mediator.Send(new GetReadModelQuery(id), cancellationToken);
                return readModel is not null ? Results.Ok(readModel) : Results.NotFound();
            })
            .WithName("GetStockReadModel")
            .WithTags("Stock")
            .WithDescription("Gets a stock read model")
            .Produces(200)
            .Produces(404);

        app.MapGet("/api/stock/{id:Guid}/read-model/hourly", async (
                [FromRoute] Guid id,
                [FromQuery] DateTimeOffset from,
                [FromQuery] DateTimeOffset to,
                [FromQuery] TimeOnly time,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var readModel = await mediator.Send(new GetHourlyReadModelQuery(id, from, to, time), cancellationToken);
                return readModel is not null ? Results.Ok(readModel) : Results.NotFound();
            })
            .WithName("GetStockHourlyReadModel")
            .WithTags("Stock")
            .WithDescription("Gets the hourly price-change read model for a stock")
            .Produces(200)
            .Produces(404);

        app.MapGet("/api/stock/{id:Guid}/read-model/daily", async (
                [FromRoute] Guid id,
                [FromQuery] DateOnly date,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var readModel = await mediator.Send(new GetDailyReadModelQuery(id, date), cancellationToken);
                return readModel is not null ? Results.Ok(readModel) : Results.NotFound();
            })
            .WithName("GetStockDailyReadModel")
            .WithTags("Stock")
            .WithDescription("Gets the daily OHLC read model for a stock")
            .Produces(200)
            .Produces(404);

        app.MapGet("/api/stock/{id:Guid}/read-model/weekly", async (
                [FromRoute] Guid id,
                [FromQuery] DateOnly startDate,
                [FromQuery] DateOnly endDate,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var readModel = await mediator.Send(new GetWeeklyReadModelQuery(id, startDate, endDate), cancellationToken);
                return Results.Ok(readModel);
            })
            .WithName("GetStockWeeklyReadModel")
            .WithTags("Stock")
            .WithDescription("Gets the weekly daily-rollup read model for a stock")
            .Produces(200)
            .Produces(404);
    }
}