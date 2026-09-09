using Hangfire;
using Hangfire.Dashboard;
using Microsoft.Extensions.Internal;
using Stock.Infrastructure.Cron;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Persistence;
using Stock.Presentation.Builder;
using Stock.Presentation.Http.Controllers;
using Stock.Presentation.Kafka;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.AddResilence();
builder.AddPersistence();
builder.AddApplication();
builder.AddKafka();
builder.AddMessaging();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException(
                           "Connection string 'DefaultConnection' is not configured.");

builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(connectionString));
builder.Services.AddHangfireServer();
builder.Services.AddSingleton<ISystemClock, SystemClock>();

DbMigrator.ApplyMigrations(connectionString);

var app = builder.Build();

app.MapStockController();
app.MapStockReadController();
app.MapStockMetadataController();

// dev only
if (app.Environment.IsDevelopment())
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new AllowAllDashboardAuthorizationFilter()]
    });
app.UseCronJobs();

if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseHttpsRedirection();

app.Run();

file class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        return true;
    }
}