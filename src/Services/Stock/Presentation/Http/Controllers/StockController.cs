using MediatR;
using Microsoft.AspNetCore.Mvc;
using Stock.Application.Commands.ChangeStockName;
using Stock.Application.Commands.ChangeStockTradingTime;
using Stock.Application.Commands.CreateStock;
using Stock.Application.Commands.SetStockOpenToTrade;

namespace Stock.Presentation.Http.Controllers;

public static class StockController
{
    public static void MapStockController(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{id:Guid}", async (
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

                return await mediator.Send(command, cancellationToken) switch
                {
                    CreateStockResult.Created => Results.Created($"/api/v1/stocks/{id}/metadata", new { Id = id }),
                    CreateStockResult.AlreadyExists => Results.Ok(new { Id = id }),
                    _ => Results.Conflict()
                };
            })
            .WithName("CreateStock")
            .WithDescription(
                "Creates a stock's metadata record under a client-generated id, repeating it returns 200, repeating with a different body returns 409, Location points to the strongly consistent metadata")
            .Produces(201)
            .Produces(200)
            .ProducesValidationProblem()
            .ProducesProblem(409);

        app.MapPatch("/{id:Guid}/name", async (
                [FromRoute] Guid id,
                [FromBody] ChangeStockNameRequest request,
                [FromServices] IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var found = await mediator.Send(new ChangeStockNameCommand(id, request.Name), cancellationToken);
                return found ? Results.NoContent() : Results.NotFound();
            })
            .WithName("ChangeStockName")
            .WithDescription("Changes a stock's name")
            .Produces(204)
            .ProducesValidationProblem()
            .ProducesProblem(404);

        app.MapPatch("/{id:Guid}/trading-time", async (
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
            .WithDescription("Changes a stock's trading hours")
            .Produces(204)
            .ProducesValidationProblem()
            .ProducesProblem(404);

        app.MapPut("/{id:Guid}/open-to-trade", async (
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
            .WithDescription("Sets whether a stock is open to trade")
            .Produces(204)
            .ProducesValidationProblem()
            .ProducesProblem(404);
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
