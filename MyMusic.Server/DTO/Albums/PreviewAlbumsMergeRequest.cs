namespace MyMusic.Server.DTO.Albums;

public record PreviewAlbumsMergeRequest
{
    public required long TargetId { get; init; }
    public required List<long> SourceIds { get; init; }
}
