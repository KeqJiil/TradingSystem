using Hangfire;
using Stock.Infrastructure.Persistence;
using Stock.Presentation.Builder;
using Stock.Presentation.Http.Controllers;
using Stock.Presentation.Kafka;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.AddKafka();
builder.AddResilence();
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(
        builder.Configuration.GetConnectionString("Default")));


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException(
                           "Connection string 'DefaultConnection' is not configured.");

DbMigrator.ApplyMigrations(connectionString);

var app = builder.Build();

app.MapStockReadController();
app.UseHangfireDashboard();

if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseHttpsRedirection();

app.Run();