using System.Text.Json;

namespace Stock.Infrastructure.Serialization;

public static class JsonExtensions
{
    public static T? TryDeserialize<T>(this string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}