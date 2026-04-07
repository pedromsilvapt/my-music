using MyMusic.Common.Services.AuditRules;
using MyMusic.Server.DTO.Songs;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Audits;

public record GetSoundalikeDuplicatesResponse
{
    public required List<SoundalikeDuplicateGroup> Groups { get; init; }
}

public record SoundalikeDuplicateGroup
{
    public required long NonConformityId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required double MatchScore { get; init; }
    public required List<SoundalikeSongItem> Songs { get; init; }
    public long? PrimarySongId { get; init; }
    public Dictionary<long, SecondaryAction> SecondaryActions { get; init; } = new();
}

public record SoundalikeSongItem
{
    public required long Id { get; init; }
    public required long? Cover { get; init; }
    public required int? CoverWidth { get; init; }
    public required int? CoverHeight { get; init; }
    public required string Title { get; init; }
    public required IEnumerable<ListSongsArtist> Artists { get; init; }
    public required ListSongsAlbum Album { get; init; }
    public required IEnumerable<ListSongsGenre> Genres { get; init; }
    public required int? Year { get; init; }
    public required string Duration { get; init; }
    public required int? Bitrate { get; init; }
    public required long Size { get; init; }
    public required bool HasLyrics { get; init; }
    public required bool IsExplicit { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? AddedAt { get; init; }

    public static SoundalikeSongItem FromEntity(Entities.Song song)
    {
        var artists = song.Artists.Select(a => ListSongsArtist.FromEntity(a.Artist)).ToList();
        var genres = song.Genres.Select(g => ListSongsGenre.FromEntity(g.Genre)).ToList();
        var album = ListSongsAlbum.FromEntity(song.Album);

        return new SoundalikeSongItem
        {
            Id = song.Id,
            Cover = song.CoverId,
            CoverWidth = song.Cover?.Width,
            CoverHeight = song.Cover?.Height,
            Title = song.Title,
            Artists = artists,
            Album = album,
            Genres = genres,
            Year = song.Year,
            Duration = $"{Convert.ToInt32(song.Duration.TotalMinutes)}:{song.Duration.Seconds:00}",
            Bitrate = song.Bitrate,
            Size = song.Size,
            HasLyrics = song.HasLyrics,
            IsExplicit = song.Explicit,
            CreatedAt = song.CreatedAt,
            AddedAt = song.AddedAt
        };
    }
}

public record SecondarySongAction
{
    public required long SongId { get; init; }
    public required SecondaryAction Action { get; init; }
}

public record ResolveSoundalikesRequest
{
    public required List<GroupResolution> Resolutions { get; init; }
}

public record GroupResolution
{
    public required long NonConformityId { get; init; }
    public required long PrimarySongId { get; init; }
    public required List<SecondarySongAction> SecondaryActions { get; init; }
}

public record ResolveSoundalikesResponse
{
    public required int ResolvedCount { get; init; }
}

public record ExcludeDuplicatePairRequest
{
    public required long SongAId { get; init; }
    public required long SongBId { get; init; }
    public string? Reason { get; init; }
}

public record ListExcludedPairsResponse
{
    public required List<ExcludedPairItem> Pairs { get; init; }
}

public record ExcludedPairItem
{
    public required long Id { get; init; }
    public required ListSongItem SongA { get; init; }
    public required ListSongItem SongB { get; init; }
    public required DateTime CreatedAt { get; init; }
    public string? Reason { get; init; }
}

public record UpdateSoundalikeSelectionRequest
{
    public long? PrimarySongId { get; init; }
    public Dictionary<long, SecondaryAction> SecondaryActions { get; init; } = new();
}
