using System.Text;
using Confluent.Kafka;
using Messaging.Dlq;

namespace Messaging.Tests.Dlq;

public class DlqMessageBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public void Build_PreservesKeyPayloadAndOriginalHeaders()
    {
        var (_, message) = DlqMessageBuilder.Build(Raw(), DlqReasons.DeadLetter, "boom", 3, Now);

        Assert.Equal("key-1", message.Key);
        Assert.Equal([1, 2, 3], message.Value);
        Assert.Equal("OrderCreated", Header(message, MessagingHeaders.EventType));
    }

    [Fact]
    public void Build_NullKey_StaysNull()
    {
        var (_, message) = DlqMessageBuilder.Build(Raw(key: null), DlqReasons.DeadLetter, null, 0, Now);

        Assert.Null(message.Key);
    }

    [Fact]
    public void Build_AddsDlqHeaders()
    {
        var (_, message) = DlqMessageBuilder.Build(Raw(), DlqReasons.Exhausted, "boom", 5, Now);

        Assert.Equal(DlqReasons.Exhausted, Header(message, MessagingHeaders.DlqReason));
        Assert.Equal("5", Header(message, MessagingHeaders.Attempt));
        Assert.Equal("orders", Header(message, MessagingHeaders.OriginalTopic));
        Assert.Equal(Now.ToString("O"), Header(message, MessagingHeaders.FirstFailureAt));
        Assert.Equal("boom", Header(message, MessagingHeaders.ExceptionMessage));
    }

    [Fact]
    public void Build_TopicIsGroupTopicDlq()
    {
        var (topic, _) = DlqMessageBuilder.Build(Raw(), DlqReasons.DeadLetter, null, 0, Now);

        Assert.Equal("group.orders.dlq", topic);
    }

    [Fact]
    public void Build_LongDetails_AreTruncated()
    {
        var (_, message) = DlqMessageBuilder.Build(Raw(), DlqReasons.DeadLetter, new string('x', 5_000), 0, Now);

        Assert.Equal(1_000, Header(message, MessagingHeaders.ExceptionMessage)!.Length);
    }

    [Fact]
    public void Build_NoDetails_DoesNotAddExceptionHeader()
    {
        var (_, message) = DlqMessageBuilder.Build(Raw(), DlqReasons.DeadLetter, null, 0, Now);

        Assert.Null(Header(message, MessagingHeaders.ExceptionMessage));
    }

    [Fact]
    public void Build_InboundOriginalTopicHeader_DoesNotRedirectDlq()
    {
        var headers = new Dictionary<string, string>
        {
            [MessagingHeaders.OriginalTopic] = "payments"
        };

        var (topic, message) = DlqMessageBuilder.Build(Raw(headers: headers),
            DlqReasons.DeadLetter, null, 0, Now);

        Assert.Equal("group.orders.dlq", topic);
        Assert.Equal("orders", Header(message, MessagingHeaders.OriginalTopic));
    }
    
    private static RawMessage Raw(string topic = "orders", string? key = "key-1",
        Dictionary<string, string>? headers = null)
    {
        return new RawMessage([1, 2, 3], topic, "group", "OrderCreated", 7, 2, Now,
            headers ?? new Dictionary<string, string> { [MessagingHeaders.EventType] = "OrderCreated" }, key);
    }

    private static string? Header(Message<string, byte[]> message, string name)
    {
        return message.Headers.TryGetLastBytes(name, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;
    }
}
