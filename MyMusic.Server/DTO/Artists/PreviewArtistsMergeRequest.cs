namespace MyMusic.Server.DTO.Artists;

public record PreviewArtistsMergeRequest
{
    public required long TargetId { get; init; }
    public required List<long> SourceIds { get; init; }
}
