using Microsoft.Extensions.Internal;

namespace Stock.Tests.Infrastructure.Cron;

internal class FakeSystemClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}
