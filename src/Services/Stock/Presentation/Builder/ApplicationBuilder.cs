using MediatR;

namespace Stock.Presentation.Builder;

public static class ApplicationBuilder
{
    public static void AddApplication(this WebApplicationBuilder builder)
    {
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
    }
}
