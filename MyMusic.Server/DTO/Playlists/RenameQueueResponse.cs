using MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Playlists;

public record RenameQueueResponse
{
    public required ListQueueItem Queue { get; init; }
}