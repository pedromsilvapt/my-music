using MyMusic.Common;

namespace MyMusic.Server.DTO.Songs;

public record BatchMultiUpdateSongsRequest
{
    public required List<SongMultiUpdateItem> Updates { get; set; }
}

public record SongMultiUpdateItem
{
    public required long SongId { get; set; }
    public ValueUpdate<string>? Title { get; set; }
    public StructValueUpdate<int>? Year { get; set; }
    public ValueUpdate<string>? Lyrics { get; set; }
    public StructValueUpdate<decimal>? Rating { get; set; }
    public StructValueUpdate<bool>? Explicit { get; set; }
    public ValueUpdate<ArtworkRef>? Cover { get; set; }
    public ValueUpdate<AlbumRef>? Album { get; set; }
    public ValueUpdate<List<ArtistRef>>? Artists { get; set; }
    public ValueUpdate<List<GenreRef>>? Genres { get; set; }
}

public record BatchMultiUpdateSongsResponse
{
    public required List<BatchMultiUpdateSongResult> Songs { get; set; }
}

public record BatchMultiUpdateSongResult
{
    public required long Id { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public UpdateSongItem? Song { get; set; }
}
