namespace MyMusic.Server.DTO.SongSharing;

public record ManageSongSharesResponse
{
    public int Created { get; set; }
    public int Removed { get; set; }
}