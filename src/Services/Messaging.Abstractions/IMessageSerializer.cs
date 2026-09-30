namespace Messaging.Abstractions;

public interface IMessageSerializer
{
    string Serialize<TMessage>(TMessage message);
    TMessage Deserialize<TMessage>(string message);
}