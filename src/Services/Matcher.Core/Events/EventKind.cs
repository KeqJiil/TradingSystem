namespace Matcher.Core.Events;

public enum EventKind : byte
{
    Rejected = 1,
    Accepted = 2,
    Filled = 3,
    Canceled = 4,
    CancelRejected = 5,
    Expired = 6
}