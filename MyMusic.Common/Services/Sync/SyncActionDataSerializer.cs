using System.Text.Json;

namespace MyMusic.Common.Services.Sync;

public static class SyncActionDataSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = true,
    };

    public static JsonElement Serialize<T>(T data) where T : class
    {
        return JsonSerializer.SerializeToElement(data, Options);
    }

    public static T? Deserialize<T>(JsonElement? data) where T : class
    {
        if (!data.HasValue || data.Value.ValueKind == JsonValueKind.Null)
            return null;

        return JsonSerializer.Deserialize<T>(data.Value, Options);
    }
}
