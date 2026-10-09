namespace Messaging.IntegrationTests.Support;

public sealed record OrderPlaced(string OrderId);

public sealed record OrderCancelled(string OrderId);
