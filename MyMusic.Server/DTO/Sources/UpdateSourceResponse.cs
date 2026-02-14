using AgileObjects.AgileMapper;

namespace MyMusic.Server.DTO.Sources;

using Entities = Common.Entities;

public record UpdateSourceResponse
{
    public required UpdateSourceItem Source { get; set; }

    public record UpdateSourceItem : SourceDataDto
    {
        public new static UpdateSourceItem FromEntity(Entities.Source source) =>
            Mapper.Map(source).ToANew<UpdateSourceItem>();
    }
}