using AgileObjects.AgileMapper;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Playlists;

public record UpdatePlaylistResponse
{
    public required UpdatePlaylistItem Playlist { get; init; }
}

public record UpdatePlaylistItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }

    public static UpdatePlaylistItem FromEntity(Entities.Playlist playlist) =>
        Mapper.Map(playlist).ToANew<UpdatePlaylistItem>();
}