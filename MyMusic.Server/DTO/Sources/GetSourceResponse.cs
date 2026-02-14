using AgileObjects.AgileMapper;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Sources;

public record GetSourceResponse
{
    public required GetSourceItem Source { get; set; }
}

public record GetSourceItem : SourceDto
{
    public new static GetSourceItem FromEntity(Entities.Source source) =>
        Mapper.Map(source).ToANew<GetSourceItem>();
}