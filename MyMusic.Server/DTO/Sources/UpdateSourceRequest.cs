namespace MyMusic.Server.DTO.Sources;

public record UpdateSourceRequest
{
    public required UpdateSourceData Source { get; set; }

    public record UpdateSourceData : SourceDataDto { }
}