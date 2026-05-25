using System.Globalization;
using System.Text.Json;

namespace MyMusic.Common.Services.Sync;

internal static class JsonElementExtensions
{
    internal static string? GetStringOrDefault(this JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    internal static long? GetInt64OrDefault(this JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
            return prop.GetInt64();
        return null;
    }

    internal static DateTime? GetDateTimeOrDefault(this JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            if (DateTime.TryParse(prop.GetString(), null, DateTimeStyles.RoundtripKind, out var dt))
                return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        }

        return null;
    }

    internal static string? GetStringOrDefault(this JsonElement? element, string propertyName)
    {
        if (element.HasValue && element.Value.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    internal static long? GetInt64OrDefault(this JsonElement? element, string propertyName)
    {
        if (element.HasValue && element.Value.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
            return prop.GetInt64();
        return null;
    }

    internal static DateTime? GetDateTimeOrDefault(this JsonElement? element, string propertyName)
    {
        if (element.HasValue && element.Value.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            if (DateTime.TryParse(prop.GetString(), null, DateTimeStyles.RoundtripKind, out var dt))
                return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        }

        return null;
    }
}