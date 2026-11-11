using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Order.Application;
using Order.Infrastructure.Messaging;
using Order.Infrastructure.Persistence;
using Order.Infrastructure.Persistence.Configuration;
using Order.Presentation;
using Order.Presentation.Grpc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.AddObservability();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.MapMessaging(builder.Configuration);

builder.Services.AddAuthentication().AddJwtBearer(options => options.MapInboundClaims = false);
builder.Services.AddAuthorization();

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderDbContext>("db", tags: ["ready"])
    .AddCheck<KafkaHealthCheck>("kafka", tags: ["ready"]);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGrpcService<OrderGrpcService>();
if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Database.MigrateAsync();

app.Run();
