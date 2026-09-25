using System.Text.Json;
using System.Text.Json.Serialization;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.SongHistory.Models;

public record SongSnapshot
{
    [JsonPropertyName("id")] public long Id { get; init; }

    [JsonPropertyName("title")] public required string Title { get; init; }

    [JsonPropertyName("label")] public required string Label { get; init; }

    [JsonPropertyName("album_id")] public long AlbumId { get; init; }

    [JsonPropertyName("cover_id")] public long? CoverId { get; init; }

    [JsonPropertyName("year")] public int? Year { get; init; }

    [JsonPropertyName("lyrics")] public string? Lyrics { get; init; }

    [JsonPropertyName("explicit")] public bool Explicit { get; init; }

    [JsonPropertyName("size")] public long Size { get; init; }

    [JsonPropertyName("track")] public int? Track { get; init; }

    [JsonPropertyName("duration")] public TimeSpan Duration { get; init; }

    [JsonPropertyName("bitrate")] public int? Bitrate { get; init; }

    [JsonPropertyName("owner_id")] public long OwnerId { get; init; }

    [JsonPropertyName("rating")] public decimal? Rating { get; init; }

    [JsonPropertyName("is_favorite")] public bool IsFavorite { get; init; }

    [JsonPropertyName("play_count")] public int PlayCount { get; init; }

    [JsonPropertyName("repository_path")] public required string RepositoryPath { get; init; }

    [JsonPropertyName("checksum")] public required string Checksum { get; init; }

    [JsonPropertyName("checksum_algorithm")] public required string ChecksumAlgorithm { get; init; }

    [JsonPropertyName("added_at")] public DateTime? AddedAt { get; init; }

    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; init; }

    [JsonPropertyName("modified_at")] public DateTime ModifiedAt { get; init; }

    [JsonPropertyName("file_modified_at")] public DateTime? FileModifiedAt { get; init; }

    [JsonPropertyName("action")] public required string Action { get; init; }

    [JsonPropertyName("album")] public SongSnapshotAlbum? Album { get; init; }

    [JsonPropertyName("artists")] public List<SongSnapshotArtist> Artists { get; init; } = [];

    [JsonPropertyName("genres")] public List<SongSnapshotGenre> Genres { get; init; } = [];

    [JsonPropertyName("sources")] public List<SongSnapshotSource> Sources { get; init; } = [];

    [JsonPropertyName("devices")] public List<SongSnapshotDevice> Devices { get; init; } = [];

    [JsonPropertyName("cover")] public SongSnapshotCover? Cover { get; init; }

    public static SongSnapshot Deserialize(JsonElement element)
        => element.Deserialize<SongSnapshot>(SongHistoryJsonOptions.Options)
           ?? throw new JsonException("Failed to deserialize SongSnapshot");

    public JsonElement SerializeToElement()
        => JsonSerializer.SerializeToElement(this, SongHistoryJsonOptions.Options);
}

public record SongSnapshotAlbum
{
    [JsonPropertyName("id")] public long Id { get; init; }

    [JsonPropertyName("title")] public required string Title { get; init; }

    [JsonPropertyName("artist_id")] public long? ArtistId { get; init; }

    [JsonPropertyName("artist_name")] public string? ArtistName { get; init; }
}

public record SongSnapshotArtist
{
    [JsonPropertyName("id")] public long Id { get; init; }

    [JsonPropertyName("name")] public required string Name { get; init; }
}

public record SongSnapshotGenre
{
    [JsonPropertyName("id")] public long Id { get; init; }

    [JsonPropertyName("name")] public required string Name { get; init; }
}

public record SongSnapshotSource
{
    [JsonPropertyName("id")] public long Id { get; init; }

    [JsonPropertyName("name")] public required string Name { get; init; }
}

public record SongSnapshotDevice
{
    [JsonPropertyName("id")] public long Id { get; init; }

    [JsonPropertyName("device_path")] public required string DevicePath { get; init; }

    [JsonPropertyName("sync_action")] public SongSyncAction? SyncAction { get; init; }
}

public record SongSnapshotCover
{
    [JsonPropertyName("id")] public long Id { get; init; }

    [JsonPropertyName("mime_type")] public required string MimeType { get; init; }

    [JsonPropertyName("width")] public int Width { get; init; }

    [JsonPropertyName("height")] public int Height { get; init; }

    [JsonPropertyName("data")] public required string Data { get; init; }
}