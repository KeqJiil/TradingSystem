using Google.Protobuf.WellKnownTypes;

namespace Order.Infrastructure.Messaging.Serialization;

public static class DurationExtensions
{
    public static TimeOnly ToTimeOnly(this Duration? duration)
    {
        if (duration is null)
            throw new ArgumentException("Trading time is missing.");

        return TimeOnly.FromTimeSpan(duration.ToTimeSpan());
    }
}
