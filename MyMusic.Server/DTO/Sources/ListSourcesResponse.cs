using AgileObjects.AgileMapper;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Sources;

public record ListSourcesResponse
{
    public required IEnumerable<ListSourcesItem> Sources { get; set; }
}

public record ListSourcesItem : SourceDto
{
    public new static ListSourcesItem FromEntity(Entities.Source source) =>
        Mapper.Map(source).ToANew<ListSourcesItem>();
}