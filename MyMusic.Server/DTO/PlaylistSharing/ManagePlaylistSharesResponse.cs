namespace MyMusic.Server.DTO.PlaylistSharing;

public record ManagePlaylistSharesResponse
{
    public required int Created { get; init; }
    public required int Removed { get; init; }
}
