using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Order.Application;
using Order.Infrastructure.Messaging;
using Order.Infrastructure.Persistence;
using Order.Infrastructure.Persistence.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.MapMessaging(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderDbContext>("db", tags: ["ready"])
    .AddCheck<KafkaHealthCheck>("kafka", tags: ["ready"]);

var app = builder.Build();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.Run();
