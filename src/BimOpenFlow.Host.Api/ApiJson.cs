using System.Text.Json;
using System.Text.Json.Serialization;

namespace BimOpenFlow.Host.Api;

/// <summary>
/// The one JSON configuration for the API: camelCase property names and
/// enum values as their exact names, matching the generated TS contracts
/// (string unions like "EffectPending"). Nulls are omitted so optional
/// contract fields ("string[]?") arrive as absent, not null.
/// </summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, Options);
}
