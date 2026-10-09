using System.Diagnostics;

namespace Messaging.Tests.TestSupport;

public sealed class ActivityRecorder : IDisposable
{
    private readonly ActivityListener _listener;

    public List<Activity> Stopped { get; } = new();

    public ActivityRecorder(string sourceName = MessagingTelemetry.SourceName)
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (Stopped) Stopped.Add(activity);
            }
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public Activity Single(string displayName)
    {
        return Stopped.Single(a => a.DisplayName == displayName);
    }

    public void Dispose()
    {
        _listener.Dispose();
    }
}
