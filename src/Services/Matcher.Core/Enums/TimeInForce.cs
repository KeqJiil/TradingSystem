namespace Matcher.Core.Enums;

public enum TimeInForce : byte
{
    Day = 1, // until the end of the trading day
    Gtc = 2, // until canceled
    Ioc = 3, // immediate or cancel
    Fok = 4 // fill or kill
}