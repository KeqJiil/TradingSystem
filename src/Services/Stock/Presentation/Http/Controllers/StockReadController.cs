using Microsoft.AspNetCore.Mvc;

namespace Stock.Presentation.Http.Controllers;

public static class StockReadController
{
    public static void MapStockReadController(this WebApplication app)
    {
        app.MapGet("/api/stock/{Id:Guid}", async ([FromRoute] Guid Id) =>
        {
            
        })
        .WithName("GetStockReadModel")
        .WithTags("Stock")
        .WithDescription("Gets a stock read models")
        .Produces(200)
        .Produces(404) ;
    }
}