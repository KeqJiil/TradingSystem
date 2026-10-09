namespace Messaging.Tests.TestSupport.Delivery;

public static class DeliveryContexts
{
    public static DeliveryContext<TestMessage> Sample()
    {
        return new DeliveryContext<TestMessage>(
            "topic", "group", 0, 0, "message-id", nameof(TestMessage),
            new TestMessage("value"), DateTimeOffset.UtcNow, new Dictionary<string, string>());
    }
}
