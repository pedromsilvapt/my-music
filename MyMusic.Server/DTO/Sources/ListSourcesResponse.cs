using AgileObjects.AgileMapper;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Sources;

public record ListSourcesResponse
{
    public required IEnumerable<ListSourceItem> Sources { get; set; }
}

public record ListSourceItem : SourceDto
{
    public new static ListSourceItem FromEntity(Entities.Source source) =>
        Mapper.Map(source).ToANew<ListSourceItem>();
}