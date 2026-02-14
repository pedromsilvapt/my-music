using AgileObjects.AgileMapper;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Sources;

public record DeleteSourceResponse
{
    public required DeleteSourceItem Source { get; set; }
}

public record DeleteSourceItem : SourceDto
{
    public new static DeleteSourceItem FromEntity(Entities.Source source) =>
        Mapper.Map(source).ToANew<DeleteSourceItem>();
}