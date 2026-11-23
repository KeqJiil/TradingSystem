using Google.Protobuf.WellKnownTypes;

namespace Order.Infrastructure.Messaging.Serialization;

public static class DurationExtensions
{
    public static bool TryToTimeOnly(this Duration? duration, out TimeOnly time)
    {
        time = default;
        if (duration is null)
            return false;

        var span = duration.ToTimeSpan();
        if (span < TimeSpan.Zero || span >= TimeSpan.FromHours(24))
            return false;

        time = TimeOnly.FromTimeSpan(span);
        return true;
    }
}
