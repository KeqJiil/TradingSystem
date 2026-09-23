using Hangfire.Client;
using Hangfire.Server;

namespace Stock.Infrastructure.Handlers;

public class CorrelationJobFilter : IClientFilter, IServerFilter
{
    public void OnCreating(CreatingContext filterContext)
    {
        var correlationId = CorrelationContext.CorrelationId;
        if (correlationId.HasValue)
        {
            filterContext.SetJobParameter("CorrelationId", correlationId.Value);
        }
    }

    public void OnCreated(CreatedContext filterContext)
    {
    }

    public void OnPerforming(PerformingContext filterContext)
    {
        var correlationId = filterContext.GetJobParameter<Guid?>("CorrelationId");
        if (correlationId.HasValue)
        {
            CorrelationContext.CorrelationId = correlationId;
        }
    }

    public void OnPerformed(PerformedContext filterContext)
    {
        CorrelationContext.CorrelationId = null;
    }
}