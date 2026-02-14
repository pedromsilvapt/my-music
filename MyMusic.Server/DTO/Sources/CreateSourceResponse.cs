using AgileObjects.AgileMapper;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Sources;

public record CreateSourceResponse
{
    public required CreateSourceItem Source { get; set; }
}

public record CreateSourceItem : SourceDto
{
    public new static CreateSourceItem FromEntity(Entities.Source source) =>
        Mapper.Map(source).ToANew<CreateSourceItem>();
}