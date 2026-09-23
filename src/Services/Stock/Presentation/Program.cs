using Hangfire;
using Hangfire.Dashboard;
using Microsoft.Extensions.Internal;
using Stock.Infrastructure.Cron;
using Stock.Infrastructure.Handlers;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Persistence;
using Stock.Presentation.Builder;
using Stock.Presentation.Builder.Observability;
using Stock.Presentation.Http.Controllers;
using Stock.Presentation.Http.ExceptionHandlers;
using Stock.Presentation.Http.Middlewares;
using Stock.Presentation.Kafka;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.AddResilence();
builder.AddPersistence();
builder.AddApplication();
builder.AddKafka();
builder.AddMessaging();
builder.AddObservability();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException(
                           "Connection string 'DefaultConnection' is not configured.");

builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(connectionString).UseFilter(new CorrelationJobFilter()));
builder.Services.AddHangfireServer();
builder.Services.AddSingleton<ISystemClock, SystemClock>();

builder.Services.AddHealthChecks().AddCheck<KafkaHealthCheck>("kafka", tags: ["ready"]).AddSqlServer(
    builder.Configuration.GetConnectionString("DefaultConnection")!,
    name: "sqlserver",
    tags: ["ready"]);

DbMigrator.ApplyMigrations(connectionString);

var app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();

app.UseExceptionHandler();

// dev only
if (app.Environment.IsDevelopment())
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new AllowAllDashboardAuthorizationFilter()]
    });
app.UseCronJobs();

if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseHttpsRedirection();

app.MapStockController();
app.MapStockReadController();
app.MapStockMetadataController();
app.MapHealthChecksController();

app.Run();

file class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        return true;
    }
}