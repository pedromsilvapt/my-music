using AgileObjects.AgileMapper;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Playlists;

public record CreatePlaylistResponse
{
    public required CreatePlaylistItem Playlist { get; init; }
}

public record CreatePlaylistItem
{
    public required long Id { get; init; }
    public required string Name { get; init; }

    public static CreatePlaylistItem FromEntity(Entities.Playlist playlist) =>
        Mapper.Map(playlist).ToANew<CreatePlaylistItem>();
}