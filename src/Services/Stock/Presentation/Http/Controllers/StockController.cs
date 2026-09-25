using MediatR;
using Microsoft.AspNetCore.Mvc;
using Stock.Application.Commands.ChangeStockName;
using Stock.Application.Commands.ChangeStockTradingTime;
using Stock.Application.Commands.CreateStock;
using Stock.Application.Commands.SetStockOpenToTrade;

namespace Stock.Presentation.Http.Controllers;

public static class StockController
{
    public static void MapStockController(this WebApplication app)
    {
        app.MapPut("/api/stock/{id:Guid}", async (
                [FromRoute] Guid id,
                [FromBody] CreateStockRequest request,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateStockCommand(
                    id,
                    request.Name,
                    request.IsOpenToTrade,
                    request.Currency,
                    request.TradingStartTime,
                    request.TradingEndTime);

                var created = await mediator.Send(command, cancellationToken);

                return created
                    ? Results.Created($"/api/stock/{id}/metadata", new { Id = id })
                    : Results.Ok(new { Id = id });
            })
            .WithName("CreateStock")
            .WithTags("Stock")
            .WithDescription("Creates a stock's metadata record under a client-generated id, repeating it returns 200")
            .Produces(201)
            .Produces(200)
            .Produces(400);

        app.MapPatch("/api/stock/{id:Guid}/name", async (
                [FromRoute] Guid id,
                [FromBody] ChangeStockNameRequest request,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var found = await mediator.Send(new ChangeStockNameCommand(id, request.Name), cancellationToken);
                return found ? Results.NoContent() : Results.NotFound();
            })
            .WithName("ChangeStockName")
            .WithTags("Stock")
            .WithDescription("Changes a stock's name")
            .Produces(204)
            .Produces(400)
            .Produces(404);

        app.MapPatch("/api/stock/{id:Guid}/trading-time", async (
                [FromRoute] Guid id,
                [FromBody] ChangeStockTradingTimeRequest request,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var found = await mediator.Send(
                    new ChangeStockTradingTimeCommand(id, request.OpenTime, request.CloseTime), cancellationToken);
                return found ? Results.NoContent() : Results.NotFound();
            })
            .WithName("ChangeStockTradingTime")
            .WithTags("Stock")
            .WithDescription("Changes a stock's trading hours")
            .Produces(204)
            .Produces(400)
            .Produces(404);

        app.MapPut("/api/stock/{id:Guid}/open-to-trade", async (
                [FromRoute] Guid id,
                [FromBody] SetStockOpenToTradeRequest request,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var found = await mediator.Send(new SetStockOpenToTradeCommand(id, request.IsOpenToTrade),
                    cancellationToken);
                return found ? Results.NoContent() : Results.NotFound();
            })
            .WithName("SetStockOpenToTrade")
            .WithTags("Stock")
            .WithDescription("Sets whether a stock is open to trade")
            .Produces(204)
            .Produces(400)
            .Produces(404);
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

public record SetStockOpenToTradeRequest(bool IsOpenToTrade);
