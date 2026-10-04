namespace Messaging.Tests.TestSupport.Messages;

public sealed record TestMessage(string Value);

public sealed record TestMessage2(string Value);

public sealed record SampleMessage(string Name, int Value);
