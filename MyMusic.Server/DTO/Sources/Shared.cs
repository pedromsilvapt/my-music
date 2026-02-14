using AgileObjects.AgileMapper;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Sources;

public record SourceDataDto
{
    public required string Icon { get; set; }
    public required string Name { get; set; }
    public required string Address { get; set; }
    public required bool IsPaid { get; set; }

    public static SourceDto FromEntity(Entities.Source source) =>
        Mapper.Map(source).ToANew<SourceDto>();
}

public record SourceDto : SourceDataDto
{
    public required long Id { get; set; }

    public new static SourceDto FromEntity(Entities.Source source) =>
        Mapper.Map(source).ToANew<SourceDto>();
}