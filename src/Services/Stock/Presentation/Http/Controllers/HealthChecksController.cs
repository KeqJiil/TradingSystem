using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Stock.Presentation.Http.Controllers;

public static class HealthChecksController
{
    public static void MapHealthChecksController(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });
        
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });
    }
}