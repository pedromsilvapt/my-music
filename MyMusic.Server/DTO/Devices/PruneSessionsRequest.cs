namespace MyMusic.Server.DTO.Devices;

public record PruneSessionsRequest
{
    public bool All { get; init; }
}