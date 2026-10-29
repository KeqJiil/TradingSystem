using Microsoft.EntityFrameworkCore;
using Order.Application.Abstractions;
using Order.Infrastructure.Messaging;
using Order.Infrastructure.Persistence.Configuration;
using Order.Infrastructure.Persistence.Implementations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<OrderDbContext>(cfg =>
    cfg.UseSqlServer(builder.Configuration.GetConnectionString("OrderDbConnectionString"), sqlOptions =>
    {
        sqlOptions.MigrationsAssembly(typeof(OrderDbContext).Assembly.FullName);
        sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
    }));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IOrderReader, OrderReader>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.MapMessaging();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderDbContext>("db");

var app = builder.Build();

app.Run();