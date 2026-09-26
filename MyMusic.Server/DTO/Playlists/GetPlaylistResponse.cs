using MyMusic.Common.Entities;
using MyMusic.Server.DTO.Songs;
using SongEntity = MyMusic.Common.Entities.Song;

namespace MyMusic.Server.DTO.Playlists;

public record GetPlaylistResponse
{
    public required GetPlaylistItem Playlist { get; init; }
}

public record GetPlaylistItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public required PlaylistType Type { get; init; }
    public long? CurrentSongId { get; init; }
    public required List<GetPlaylistSongItem> Songs { get; init; }
    public required long OwnerId { get; init; }
    public required string OwnerName { get; init; }

    /// <summary>True when the playlist is owned by another user and shared with the caller (read-only).</summary>
    public required bool IsSharedWithMe { get; init; }

    /// <summary>Number of users the playlist is shared with. Only reported to the owner (0 otherwise).</summary>
    public required int SharedWithCount { get; init; }

    /// <summary>
    /// Maps a <see cref="Playlist"/> (with <c>Owner</c>, <c>PlaylistSharings</c> and song metadata
    /// loaded) to a <see cref="GetPlaylistItem"/>. For recipients, only the songs owned by the
    /// playlist owner are returned, since those are the only ones shared.
    /// </summary>
    public static GetPlaylistItem FromEntity(Playlist playlist, long currentUserId)
    {
        var isOwner = playlist.OwnerId == currentUserId;

        return new GetPlaylistItem
        {
            Id = playlist.Id,
            Name = playlist.Name,
            Type = playlist.Type,
            CurrentSongId = playlist.CurrentSongId,
            Songs = playlist.PlaylistSongs
                .Where(ps => isOwner || ps.Song.OwnerId == playlist.OwnerId)
                .OrderBy(ps => ps.Order)
                .Select((ps, index) => GetPlaylistSongItem.FromEntity(ps.Song, index + 1, ps.AddedAt, ps.StopAfterPlayback, ps.SkipNextPlayback, currentUserId))
                .ToList(),
            OwnerId = playlist.OwnerId,
            OwnerName = playlist.Owner.Name,
            IsSharedWithMe = !isOwner,
            SharedWithCount = isOwner ? playlist.PlaylistSharings.Count : 0,
        };
    }
}

public record GetPlaylistSongItem : ListSongItem
{
    public required int Order { get; init; }
    public DateTime? AddedAtPlaylist { get; init; }
    public required bool StopAfterPlayback { get; init; }
    public required bool SkipNextPlayback { get; init; }

    public static GetPlaylistSongItem FromEntity(SongEntity song, int displayOrder, DateTime addedAt, bool stopAfterPlayback, bool skipNextPlayback, long currentUserId) =>
        new()
        {
            Id = song.Id,
            Cover = song.CoverId,
            Title = song.Title,
            Artists = song.Artists.Select(a => ListSongsArtist.FromEntity(a.Artist)).ToList(),
            Album = ListSongsAlbum.FromEntity(song.Album),
            Genres = song.Genres.Select(g => ListSongsGenre.FromEntity(g.Genre)).ToList(),
            Year = song.Year,
            Duration = $"{Convert.ToInt32(song.Duration.TotalMinutes)}:{song.Duration.Seconds:00}",
            Devices = song.Devices.Select(d => ListSongsDevice.FromEntity(d.Device)).DistinctBy(d => d.Id).ToList(),
            IsFavorite = false,
            IsExplicit = song.Explicit,
            HasLyrics = song.HasLyrics,
            IsShared = song.OwnerId != currentUserId,
            CreatedAt = song.CreatedAt,
            AddedAt = song.AddedAt,
            Order = displayOrder,
            AddedAtPlaylist = addedAt,
            StopAfterPlayback = stopAfterPlayback,
            SkipNextPlayback = skipNextPlayback,
        };
}