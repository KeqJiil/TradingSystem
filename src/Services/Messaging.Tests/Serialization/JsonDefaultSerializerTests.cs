using Messaging.Serializers;

namespace Messaging.Tests.Serialization;

public class JsonDefaultSerializerTests
{
    private record TestRecord(string Name, int Age, TestRecord? Nested = null);

    private record Node
    {
        public Node? Next { get; set; }
    }

    [Fact]
    public void RoundTrip_ObjectSurvivesSerializeDeserialize()
    {
        var serializer = new JsonDefaultSerializer();

        var original = new TestRecord("Alice", 30);

        var serialized = serializer.Serialize(original);
        var deserialized = serializer.Deserialize<TestRecord>(serialized.Message);
        Assert.Equal(original, deserialized.Message);
    }

    [Fact]
    public void Deserialize_InvalidJson_ReturnsFailureWithError()
    {
        var serializer = new JsonDefaultSerializer();

        var original = new TestRecord("Alice", 30);

        var serialized = serializer.Serialize(original);
        serialized.Message[0] = 0xFF;

        var deserialized = serializer.Deserialize<TestRecord>(serialized.Message);
        Assert.False(deserialized.Success);
    }

    [Fact]
    public void Deserialize_EmptyPayload_ReturnsFailure()
    {
        var serializer = new JsonDefaultSerializer();

        var deserialized = serializer.Deserialize<TestRecord>(Array.Empty<byte>());
        Assert.False(deserialized.Success);
    }

    [Fact]
    public void Deserialize_JsonNull_Behavior()
    {
        var serializer = new JsonDefaultSerializer();

        var serialized = serializer.Serialize<TestRecord>(null);
        var deserialized = serializer.Deserialize<TestRecord>(serialized.Message);
        Assert.True(deserialized.Success);
        Assert.Null(deserialized.Message);
    }

    [Fact]
    public void Serialize_CircularReference_ReturnsFailure()
    {
        var circular = new Node();
        circular.Next = circular;

        var result = new JsonDefaultSerializer().Serialize(circular);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Empty(result.Message);
    }
}