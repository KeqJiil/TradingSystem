using MediatR;
using Microsoft.AspNetCore.Mvc;
using Stock.Application.Commands.ChangeStockName;
using Stock.Application.Commands.ChangeStockTradingTime;
using Stock.Application.Commands.CreateStock;
using Stock.Application.Commands.ToggleStockOpenToTrade;

namespace Stock.Presentation.Http.Controllers;

public static class StockController
{
    public static void MapStockController(this WebApplication app)
    {
        app.MapPost("/api/stock", async (
                [FromBody] CreateStockRequest request,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateStockCommand(
                    request.Name,
                    request.IsOpenToTrade,
                    request.Currency,
                    request.TradingStartTime,
                    request.TradingEndTime);

                var id = await mediator.Send(command, cancellationToken);

                return Results.Created($"/api/stock/{id}/metadata", new { Id = id });
            })
            .WithName("CreateStock")
            .WithTags("Stock")
            .WithDescription("Creates a stock's metadata record")
            .Produces(201)
            .Produces(400);

        app.MapPatch("/api/stock/{id:Guid}/name", async (
                [FromRoute] Guid id,
                [FromBody] ChangeStockNameRequest request,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                await mediator.Send(new ChangeStockNameCommand(id, request.Name), cancellationToken);
                return Results.NoContent();
            })
            .WithName("ChangeStockName")
            .WithTags("Stock")
            .WithDescription("Changes a stock's name")
            .Produces(204)
            .Produces(400);

        app.MapPatch("/api/stock/{id:Guid}/trading-time", async (
                [FromRoute] Guid id,
                [FromBody] ChangeStockTradingTimeRequest request,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                await mediator.Send(
                    new ChangeStockTradingTimeCommand(id, request.OpenTime, request.CloseTime), cancellationToken);
                return Results.NoContent();
            })
            .WithName("ChangeStockTradingTime")
            .WithTags("Stock")
            .WithDescription("Changes a stock's trading hours")
            .Produces(204)
            .Produces(400);

        app.MapPost("/api/stock/{id:Guid}/toggle-open-to-trade", async (
                [FromRoute] Guid id,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                await mediator.Send(new ToggleStockOpenToTradeCommand(id), cancellationToken);
                return Results.NoContent();
            })
            .WithName("ToggleStockOpenToTrade")
            .WithTags("Stock")
            .WithDescription("Toggles whether a stock is open to trade")
            .Produces(204)
            .Produces(400);
    }
}

public record CreateStockRequest(
    string Name,
    bool IsOpenToTrade,
    string Currency,
    TimeOnly TradingStartTime,
    TimeOnly TradingEndTime);

public record ChangeStockNameRequest(string Name);

public record ChangeStockTradingTimeRequest(TimeOnly OpenTime, TimeOnly CloseTime);
