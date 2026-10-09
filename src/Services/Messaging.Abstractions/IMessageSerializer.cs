namespace Messaging.Abstractions;

public interface IMessageSerializer
{
    /// <summary>
    /// Serializes a message of type TMessage into a byte array.
    /// </summary>
    /// <param name="message">The message to serialize</param>
    /// <typeparam name="TMessage">The type of the message</typeparam>
    /// <returns>The result of the serialization operation</returns>
    SerializationResult<byte[]> Serialize<TMessage>(TMessage message);

    /// <summary>
    /// Deserializes a byte array into a message of type TMessage.
    /// </summary>
    /// <param name="message">The byte array to deserialize</param>
    /// <typeparam name="TMessage">The type of the message</typeparam>
    /// <returns>The result of the deserialization operation</returns>
    SerializationResult<TMessage> Deserialize<TMessage>(byte[] message);
}

/// <summary>
/// Represents the result of a serialization or deserialization operation.
/// </summary>
/// <param name="Message">The message resulting from the operation</param>
/// <param name="Success">Indicates whether the operation was successful</param>
/// <param name="Error">The error message if the operation failed</param>
/// <typeparam name="T">The type of the message</typeparam>
public record struct SerializationResult<T>(T Message, bool Success, string? Error = null);