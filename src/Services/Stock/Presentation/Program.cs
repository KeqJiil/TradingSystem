using Stock.Infrastructure.Persistence;
using Stock.Presentation.Http.Controllers;
using Stock.Presentation.Kafka;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.AddKafka();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

DbMigrator.ApplyMigrations(connectionString);

var app = builder.Build();

app.MapStockReadController();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
