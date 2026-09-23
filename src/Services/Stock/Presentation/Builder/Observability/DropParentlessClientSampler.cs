using System.Diagnostics;
using OpenTelemetry.Trace;

namespace Stock.Presentation.Builder.Observability;

public sealed class DropParentlessClientSampler : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters p)
    {
        return p.ParentContext.TraceId == default && p.Kind == ActivityKind.Client
            ? new SamplingResult(SamplingDecision.Drop)
            : new SamplingResult(SamplingDecision.RecordAndSample);
    }
}