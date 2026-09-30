using MediatR;
using Microsoft.AspNetCore.Mvc;
using Stock.Application.Abstractions;
using Stock.Application.Queries.GetDailyReadModel;
using Stock.Application.Queries.GetHourlyReadModel;
using Stock.Application.Queries.GetReadModel;
using Stock.Application.Queries.GetWeeklyReadModel;

namespace Stock.Presentation.Http.Controllers;

public static class StockReadController
{
    public static void MapStockReadController(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id:Guid}", async (
                [FromRoute] Guid id,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var readModel = await mediator.Send(new GetReadModelQuery(id), cancellationToken);
                return readModel is not null ? Results.Ok(readModel) : Results.NotFound();
            })
            .WithName("GetStock")
            .WithDescription("Gets a stock with its current price, eventually consistent with writes")
            .Produces<StockReadModel>(200)
            .ProducesProblem(404);

        app.MapGet("/{id:Guid}/prices/hourly", async (
                [FromRoute] Guid id,
                [FromQuery] DateTimeOffset from,
                [FromQuery] DateTimeOffset to,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var readModel = await mediator.Send(new GetHourlyReadModelQuery(id, from, to), cancellationToken);
                return Results.Ok(readModel);
            })
            .WithName("GetStockHourlyPrices")
            .WithDescription("Gets hourly candles in [from, to), at most 7 days, empty list when there is no data")
            .Produces<HourlyReadModel>(200)
            .ProducesValidationProblem();

        app.MapGet("/{id:Guid}/prices/daily/{date}", async (
                [FromRoute] Guid id,
                [FromRoute] DateOnly date,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var readModel = await mediator.Send(new GetDailyReadModelQuery(id, date), cancellationToken);
                return readModel is not null ? Results.Ok(readModel) : Results.NotFound();
            })
            .WithName("GetStockDayPrice")
            .WithDescription("Gets the daily OHLC candle for a date")
            .Produces<DailyReadModel>(200)
            .ProducesValidationProblem()
            .ProducesProblem(404);

        app.MapGet("/{id:Guid}/prices/daily", async (
                [FromRoute] Guid id,
                [FromQuery] DateOnly from,
                [FromQuery] DateOnly to,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var readModel = await mediator.Send(new GetWeeklyReadModelQuery(id, from, to), cancellationToken);
                return Results.Ok(readModel);
            })
            .WithName("GetStockDailyPrices")
            .WithDescription("Gets daily candles in [from, to), at most 366 days, empty list when there is no data")
            .Produces<WeeklyReadModel>(200)
            .ProducesValidationProblem();
    }
}