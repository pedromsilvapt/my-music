using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.PlayHistory;

public record ListPlayHistoryResponse
{
    public required List<ListPlayHistoryItem> Items { get; init; }
}

public record ListPlayHistoryItem
{
    public required long Id { get; init; }
    public required long SongId { get; init; }
    public required string SongTitle { get; init; }
    public required long AlbumId { get; init; }
    public required string AlbumName { get; init; }
    public long? CoverId { get; init; }
    public long? ArtistId { get; init; }
    public string? ArtistName { get; init; }
    public long? DeviceId { get; init; }
    public string? DeviceName { get; init; }
    public required DateTime PlayedAt { get; init; }

    public static ListPlayHistoryItem FromEntity(Entities.PlayHistory playHistory)
    {
        var songArtist = playHistory.Song.Artists.FirstOrDefault();
        return new ListPlayHistoryItem
        {
            Id = playHistory.Id,
            SongId = playHistory.SongId,
            SongTitle = playHistory.Song.Title,
            AlbumId = playHistory.Song.AlbumId,
            AlbumName = playHistory.Song.Album.Name,
            CoverId = playHistory.Song.CoverId,
            ArtistId = songArtist?.Artist?.Id,
            ArtistName = songArtist?.Artist?.Name,
            DeviceId = playHistory.DeviceId,
            DeviceName = playHistory.Device?.Name,
            PlayedAt = playHistory.PlayedAt,
        };
    }
}