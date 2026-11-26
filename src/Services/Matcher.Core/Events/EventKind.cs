namespace Matcher.Core.Events;

public enum EventKind : byte
{
    Reject = 1,
    Accept = 2,
    Fill = 3,
    Cancel = 4
}