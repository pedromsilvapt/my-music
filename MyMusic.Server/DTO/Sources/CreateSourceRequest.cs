namespace MyMusic.Server.DTO.Sources;

public record CreateSourceRequest
{
    public required CreateSourceData Source { get; set; }

    public record CreateSourceData : SourceDataDto { }
}