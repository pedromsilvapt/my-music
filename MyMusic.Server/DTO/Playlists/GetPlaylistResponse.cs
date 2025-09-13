using MyMusic.Server.DTO.Songs;

namespace MyMusic.Server.DTO.Playlists;

public record GetPlaylistResponse
{
    public required List<PlaylistSong> Songs { get; set; }

    public record PlaylistSong : ListSongsResponse.Song
    {
        public required double Rank { get; set; }

        public required int Order { get; set; }
    }
}