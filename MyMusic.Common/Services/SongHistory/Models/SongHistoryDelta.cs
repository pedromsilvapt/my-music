using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyMusic.Common.Services.SongHistory.Models;

public record SongHistoryDelta
{
    [JsonPropertyName("action")] public string? Action { get; init; }

    [JsonPropertyName("title")] public FieldChange<string>? Title { get; init; }

    [JsonPropertyName("label")] public FieldChange<string>? Label { get; init; }

    [JsonPropertyName("album_id")] public FieldChange<long>? AlbumId { get; init; }

    [JsonPropertyName("cover_id")] public FieldChange<long?>? CoverId { get; init; }

    [JsonPropertyName("year")] public FieldChange<int?>? Year { get; init; }

    [JsonPropertyName("lyrics")] public FieldChange<string?>? Lyrics { get; init; }

    [JsonPropertyName("explicit")] public FieldChange<bool>? Explicit { get; init; }

    [JsonPropertyName("size")] public FieldChange<long>? Size { get; init; }

    [JsonPropertyName("track")] public FieldChange<int?>? Track { get; init; }

    [JsonPropertyName("duration")] public FieldChange<TimeSpan>? Duration { get; init; }

    [JsonPropertyName("owner_id")] public FieldChange<long>? OwnerId { get; init; }

    [JsonPropertyName("bitrate")] public FieldChange<int?>? Bitrate { get; init; }

    [JsonPropertyName("rating")] public FieldChange<decimal?>? Rating { get; init; }

    [JsonPropertyName("is_favorite")] public FieldChange<bool>? IsFavorite { get; init; }

    [JsonPropertyName("play_count")] public FieldChange<int>? PlayCount { get; init; }

    [JsonPropertyName("repository_path")] public FieldChange<string>? RepositoryPath { get; init; }

    [JsonPropertyName("checksum")] public FieldChange<string>? Checksum { get; init; }

    [JsonPropertyName("checksum_algorithm")] public FieldChange<string>? ChecksumAlgorithm { get; init; }

    [JsonPropertyName("added_at")] public FieldChange<DateTime?>? AddedAt { get; init; }

    [JsonPropertyName("created_at")] public FieldChange<DateTime>? CreatedAt { get; init; }

    [JsonPropertyName("modified_at")] public FieldChange<DateTime>? ModifiedAt { get; init; }

    [JsonPropertyName("file_modified_at")] public FieldChange<DateTime?>? FileModifiedAt { get; init; }

    [JsonPropertyName("album")] public FieldChange<SongSnapshotAlbum?>? Album { get; init; }

    [JsonPropertyName("artists")] public FieldChange<List<SongSnapshotArtist>>? Artists { get; init; }

    [JsonPropertyName("genres")] public FieldChange<List<SongSnapshotGenre>>? Genres { get; init; }

    [JsonPropertyName("sources")] public FieldChange<List<SongSnapshotSource>>? Sources { get; init; }

    [JsonPropertyName("devices")] public FieldChange<List<SongSnapshotDevice>>? Devices { get; init; }

    [JsonPropertyName("cover")] public FieldChange<SongSnapshotCover?>? Cover { get; init; }

    public static SongHistoryDelta Deserialize(JsonElement element)
        => element.Deserialize<SongHistoryDelta>(SongHistoryJsonOptions.Options)
           ?? throw new JsonException("Failed to deserialize SongHistoryDelta");

    public JsonElement SerializeToElement()
        => JsonSerializer.SerializeToElement(this, SongHistoryJsonOptions.Options);
}

public record FieldChange<T>
{
    [JsonPropertyName("old")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public T? Old { get; init; }

    [JsonPropertyName("new")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public T? New { get; init; }
}