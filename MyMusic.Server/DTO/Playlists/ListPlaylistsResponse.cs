using MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Playlists;

public record ListPlaylistsResponse
{
    public required List<ListPlaylistItem> Playlists { get; set; }
}

public record ListPlaylistItem
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public required PlaylistType Type { get; set; }
    public required int SongCount { get; set; }
    public required List<long> SongIds { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public required long OwnerId { get; set; }
    public required string OwnerName { get; set; }

    /// <summary>True when the playlist is owned by another user and shared with the caller.</summary>
    public required bool IsSharedWithMe { get; set; }

    /// <summary>Number of users the playlist is shared with. Only reported to the owner (0 otherwise).</summary>
    public required int SharedWithCount { get; set; }

    /// <summary>
    /// Maps a <see cref="Playlist"/> (with <c>Owner</c>, <c>PlaylistSongs</c> and
    /// <c>PlaylistSharings</c> loaded) to a <see cref="ListPlaylistItem"/>. For recipients, only the
    /// songs owned by the playlist owner are reported, since those are the only ones shared.
    /// </summary>
    public static ListPlaylistItem FromEntity(Playlist playlist, long currentUserId)
    {
        var isOwner = playlist.OwnerId == currentUserId;
        var songs = isOwner
            ? playlist.PlaylistSongs
            : playlist.PlaylistSongs.Where(ps => ps.Song.OwnerId == playlist.OwnerId).ToList();

        return new ListPlaylistItem
        {
            Id = playlist.Id,
            Name = playlist.Name,
            Type = playlist.Type,
            SongCount = songs.Count,
            SongIds = songs.Select(ps => ps.SongId).ToList(),
            CreatedAt = playlist.CreatedAt,
            ModifiedAt = playlist.ModifiedAt,
            OwnerId = playlist.OwnerId,
            OwnerName = playlist.Owner.Name,
            IsSharedWithMe = !isOwner,
            SharedWithCount = isOwner ? playlist.PlaylistSharings.Count : 0,
        };
    }
}