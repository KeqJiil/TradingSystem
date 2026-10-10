namespace Messaging.Tests.TestSupport.Delivery;

public static class DeliveryContexts
{
    public static DeliveryContext<TestMessage> Sample()
    {
        return new DeliveryContext<TestMessage>(
            "topic", "group", 0, 0, "message-id", nameof(TestMessage),
            new TestMessage("value"), DateTimeOffset.UtcNow, new Dictionary<string, string>());
    }

    public static DeliveryContext<TestMessage2> Sample2()
    {
        return new DeliveryContext<TestMessage2>(
            "topic", "group", 0, 0, "message-id", nameof(TestMessage2),
            new TestMessage2("value"), DateTimeOffset.UtcNow, new Dictionary<string, string>());
    }
}
