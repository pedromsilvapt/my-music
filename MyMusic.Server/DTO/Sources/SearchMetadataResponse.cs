using MyMusic.Common.Sources;

namespace MyMusic.Server.DTO.Sources;

public record SearchMetadataResponse
{
    public required List<SearchMetadataResult> Results { get; set; }
}

public record SearchMetadataResult
{
    public required long SourceId { get; set; }
    public required string SourceName { get; set; }
    public required string SourceIcon { get; set; }
    public required SourceSong Song { get; set; }
}