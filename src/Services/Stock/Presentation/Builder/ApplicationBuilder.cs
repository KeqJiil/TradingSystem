using MediatR;
using Stock.Application.Services;
using Stock.Infrastructure.MediatrPipelines;

namespace Stock.Presentation.Builder;

public static class ApplicationBuilder
{
    public static void AddApplication(this WebApplicationBuilder builder)
    {
        builder.Services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
            cfg.AddOpenBehavior(typeof(ResiliencePipelineBehaviour<,>));
        });

        builder.Services.AddScoped<EventStoreService>();
    }
}