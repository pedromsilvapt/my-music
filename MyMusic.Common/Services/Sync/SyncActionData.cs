using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyMusic.Common.Services.Sync;

public record CreateRemoteData
{
    [JsonPropertyName("songId")]
    public long? SongId { get; init; }

    [JsonPropertyName("checksum")]
    public string? Checksum { get; init; }

    [JsonPropertyName("algorithm")]
    public string? Algorithm { get; init; }

    [JsonPropertyName("modifiedAt")]
    [JsonConverter(typeof(SyncActionDateTimeJsonConverter))]
    public DateTime? ModifiedAt { get; init; }

    [JsonPropertyName("tempFilePath")]
    public string? TempFilePath { get; init; }

    [JsonPropertyName("createdAt")]
    [JsonConverter(typeof(SyncActionDateTimeJsonConverter))]
    public DateTime? CreatedAt { get; init; }

    [JsonPropertyName("originalFilePath")]
    public string? OriginalFilePath { get; init; }
}

public record UpdateRemoteData
{
    [JsonPropertyName("songId")]
    public long? SongId { get; init; }

    [JsonPropertyName("checksum")]
    public string? Checksum { get; init; }

    [JsonPropertyName("algorithm")]
    public string? Algorithm { get; init; }

    [JsonPropertyName("modifiedAt")]
    [JsonConverter(typeof(SyncActionDateTimeJsonConverter))]
    public DateTime? ModifiedAt { get; init; }

    [JsonPropertyName("tempFilePath")]
    public string? TempFilePath { get; init; }

    [JsonPropertyName("createdAt")]
    [JsonConverter(typeof(SyncActionDateTimeJsonConverter))]
    public DateTime? CreatedAt { get; init; }

    [JsonPropertyName("originalFilePath")]
    public string? OriginalFilePath { get; init; }
}

public record SongModifiedAtData
{
    [JsonPropertyName("songId")]
    public long? SongId { get; init; }

    [JsonPropertyName("modifiedAt")]
    [JsonConverter(typeof(SyncActionDateTimeJsonConverter))]
    public DateTime? ModifiedAt { get; init; }

    [JsonPropertyName("checksum")]
    public string? Checksum { get; init; }

    [JsonPropertyName("algorithm")]
    public string? Algorithm { get; init; }
}

public record RenameData
{
    [JsonPropertyName("previousPath")]
    public required string PreviousPath { get; init; }

    [JsonPropertyName("newPath")]
    public required string NewPath { get; init; }
}

public record ConflictData
{
    [JsonPropertyName("localModifiedAt")]
    [JsonConverter(typeof(SyncActionNonNullableDateTimeJsonConverter))]
    public required DateTime LocalModifiedAt { get; init; }

    [JsonPropertyName("serverModifiedAt")]
    [JsonConverter(typeof(SyncActionNonNullableDateTimeJsonConverter))]
    public required DateTime ServerModifiedAt { get; init; }
}

public record UpdateTimestampData
{
    [JsonPropertyName("newTimestamp")]
    [JsonConverter(typeof(SyncActionNonNullableDateTimeJsonConverter))]
    public required DateTime NewTimestamp { get; init; }

    [JsonPropertyName("songId")]
    public long? SongId { get; init; }
}

public record ErrorData
{
    [JsonPropertyName("errorMessage")]
    public required string ErrorMessage { get; init; }
}

public class SyncActionDateTimeJsonConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var dateString = reader.GetString();
            if (DateTime.TryParse(dateString, null, DateTimeStyles.RoundtripKind, out var dt))
                return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(value.Value.ToUniversalTime().ToString("O"));
        else
            writer.WriteNullValue();
    }
}

public class SyncActionNonNullableDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var dateString = reader.GetString();
            if (DateTime.TryParse(dateString, null, DateTimeStyles.RoundtripKind, out var dt))
                return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        }

        return default;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString("O"));
    }
}

