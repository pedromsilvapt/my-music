using MyMusic.Common;

namespace MyMusic.Server.DTO.Songs;

public record UpdateSongRequest
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

public record BatchUpdateSongsRequest
{
    public required List<long> SongIds { get; set; }
    public required SongPatch Patch { get; set; }
}

public record SongPatch
{
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
