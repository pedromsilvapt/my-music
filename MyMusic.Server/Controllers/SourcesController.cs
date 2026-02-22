using AgileObjects.AgileMapper.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Filters;
using MyMusic.Common.Services;
using MyMusic.Common.Sources;
using MyMusic.Server.DTO.Filters;
using MyMusic.Server.DTO.Sources;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("sources")]
public class SourcesController(ILogger<SourcesController> logger, ISourcesService sourcesService) : ControllerBase
{
    private readonly ILogger<SourcesController> _logger = logger;

    #region CRUD

    [HttpGet(Name = "ListSources")]
    public async Task<ListSourcesResponse> List(MusicDbContext context, CancellationToken cancellationToken)
    {
        var sources = await context.Sources
            .OrderBy(s => s.Name)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new ListSourcesResponse
        {
            Sources = sources.Select(ListSourcesItem.FromEntity).ToList(),
        };
    }

    [HttpGet("{id}", Name = "GetSource")]
    public async Task<ActionResult<GetSourceResponse>> Get(
        [FromServices] MusicDbContext db,
        [FromRoute] long id,
        CancellationToken cancellationToken = default
    )
    {
        var source = await db.Sources.FindAsync([id], cancellationToken);

        if (source is null)
        {
            return NotFound();
        }

        return new GetSourceResponse
        {
            Source = GetSourceItem.FromEntity(source),
        };
    }

    [HttpPost(Name = "CreateSource")]
    public async Task<CreateSourceResponse> Create(
        [FromServices] MusicDbContext db,
        [FromBody] CreateSourceRequest body,
        CancellationToken cancellationToken)
    {
        var source = new Source
        {
            Name = body.Source.Name,
            Address = body.Source.Address,
            Icon = body.Source.Icon,
            IsPaid = body.Source.IsPaid,
        };

        await db.AddAsync(source, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateSourceResponse
        {
            Source = CreateSourceItem.FromEntity(source),
        };
    }

    [HttpPut("{id}", Name = "UpdateSource")]
    public async Task<ActionResult<UpdateSourceResponse>> Update(
        [FromServices] MusicDbContext db,
        [FromRoute] long id,
        [FromBody] UpdateSourceRequest body,
        CancellationToken cancellationToken)
    {
        var source = await db.Sources.FindAsync([id], cancellationToken);

        if (source is null)
        {
            return NotFound();
        }

        body.Source.Map()
            .OnTo(source);

        db.Update(source);
        await db.SaveChangesAsync(cancellationToken);

        return new UpdateSourceResponse
        {
            Source = UpdateSourceResponse.UpdateSourceItem.FromEntity(source),
        };
    }

    [HttpDelete("{id}", Name = "DeleteSource")]
    public async Task<ActionResult<DeleteSourceResponse>> Delete(
        [FromServices] MusicDbContext db,
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var source = await db.Sources.FindAsync([id], cancellationToken);

        if (source is null)
        {
            return NotFound();
        }

        db.Remove(source);
        await db.SaveChangesAsync(cancellationToken);

        return new DeleteSourceResponse
        {
            Source = DeleteSourceItem.FromEntity(source),
        };
    }

    #endregion CRUD

    #region Operations

    #region Songs

    [HttpGet("{id}/songs/search/{query}", Name = "Search Songs")]
    public async Task<ActionResult<List<SourceSong>>> SearchSongsAsync(
        long id,
        string query,
        CancellationToken cancellationToken = default,
        [FromQuery] string? filter = null)
    {
        var source = await sourcesService.GetSourceClientAsync(id, cancellationToken);
        var results = await source.SearchSongsAsync(query, cancellationToken);

        results = InMemoryFilterBuilder.ApplyFuzzySearch(
            results,
            query,
            s => s.SearchableText).ToList();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var filterRequest = FilterDslParser.Parse(filter);
            results = InMemoryFilterBuilder.ApplyFilter(results, filterRequest).ToList();
        }

        return results;
    }

    [HttpGet("songs/filter-metadata", Name = "GetSourceSongFilterMetadata")]
    public ActionResult<FilterMetadataResponse> GetSourceSongFilterMetadata() =>
        Ok(new FilterMetadataResponse
        {
            Fields = GetSourceSongFieldMetadata(),
            Operators = FilterMetadataHelper.GetOperatorMetadata(),
        });

    private static List<FilterFieldMetadata> GetSourceSongFieldMetadata() =>
    [
        new()
        {
            Name = "title", Type = "string", Description = "Song title",
            SupportedOperators = ["eq", "neq", "contains", "startsWith", "endsWith"],
        },
        new()
        {
            Name = "year", Type = "number", Description = "Release year",
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte", "between", "in", "notIn"],
        },
        new()
        {
            Name = "explicit", Type = "boolean", Description = "Has explicit content",
            SupportedOperators = ["eq", "neq", "isTrue", "isFalse"],
        },
        new()
        {
            Name = "size", Type = "number", Description = "File size in bytes",
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte", "between"],
        },
        new()
        {
            Name = "track", Type = "number", Description = "Track number",
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte"],
        },
        new()
        {
            Name = "price", Type = "number", Description = "Price",
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte", "between"],
        },
        new()
        {
            Name = "durationSeconds", Type = "number", Description = "Duration in seconds", IsComputed = true,
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte", "between"],
        },
        new()
        {
            Name = "durationCategory", Type = "string",
            Description = "Duration category (Short: <3min, Medium: 3-6min, Long: >6min)", IsComputed = true,
            SupportedOperators = ["eq", "neq", "in", "notIn"], Values = ["Short", "Medium", "Long"],
        },
        new()
        {
            Name = "hasLyrics", Type = "boolean", Description = "Has lyrics", IsComputed = true,
            SupportedOperators = ["eq", "neq", "isTrue", "isFalse"],
        },
        new()
        {
            Name = "artistCount", Type = "number", Description = "Number of artists", IsComputed = true,
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte"],
        },
        new()
        {
            Name = "genreCount", Type = "number", Description = "Number of genres", IsComputed = true,
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte"],
        },
        new()
        {
            Name = "album.name", Type = "string", Description = "Album name",
            SupportedOperators = ["eq", "neq", "contains", "startsWith", "endsWith"],
        },
        new()
        {
            Name = "album.year", Type = "number", Description = "Album release year",
            SupportedOperators = ["eq", "neq", "gt", "gte", "lt", "lte"],
        },
    ];

    [HttpGet("{id}/songs/{songId}", Name = "Get Song")]
    public async Task<ActionResult<SourceSong>> GetSongAsync(long id, string songId,
        CancellationToken cancellationToken = default)
    {
        var source = await sourcesService.GetSourceClientAsync(id, cancellationToken);

        return await source.GetSongAsync(songId, cancellationToken);
    }

    [HttpGet("{id}/songs/purchase/{songId}", Name = "Purchase Song")]
    public async Task<ActionResult<Stream>> PurchaseSongAsync(long id, string songId,
        CancellationToken cancellationToken = default) =>
        NotFound();

    #endregion Songs

    #region Albums

    [HttpGet("{id}/albums/search/{query}", Name = "Search Albums")]
    public async Task<ActionResult<List<SourceAlbum>>> SearchAlbumsAsync(long id,
        string query, CancellationToken cancellationToken = default) =>
        NotFound();

    #endregion Albums

    #endregion Operations
}