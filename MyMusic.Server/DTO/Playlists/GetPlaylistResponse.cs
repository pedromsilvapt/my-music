using MyMusic.Server.DTO.Songs;

namespace MyMusic.Server.DTO.Playlists;

public record GetPlaylistResponse
{
    public required List<GetPlaylistSong> Songs { get; set; }
}

public record GetPlaylistSong : ListSongsItem
{
    public required double Rank { get; set; }

    public required int Order { get; set; }
}