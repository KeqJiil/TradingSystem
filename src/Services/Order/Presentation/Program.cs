using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Order.Application.Abstractions;
using Order.Infrastructure.Messaging;
using Order.Infrastructure.Persistence.Configuration;
using Order.Infrastructure.Persistence.Implementations;
using Order.Infrastructure.Persistence.Interceptors;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddScoped<MessagePublishingInterceptor>();

builder.Services.AddDbContext<OrderDbContext>((sp, cfg) =>
    cfg.UseSqlServer(builder.Configuration.GetConnectionString("OrderDbConnectionString"), sqlOptions =>
    {
        sqlOptions.MigrationsAssembly(typeof(OrderDbContext).Assembly.FullName);
        sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
    }).AddInterceptors(sp.GetRequiredService<MessagePublishingInterceptor>()));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IOrderReader, OrderReader>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.MapMessaging();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderDbContext>("db", tags: ["ready"]);

var app = builder.Build();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.Run();