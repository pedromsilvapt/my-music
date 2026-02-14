using AgileObjects.AgileMapper.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Sources;
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
        var source = await db.Sources.FindAsync([id], cancellationToken: cancellationToken);

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
        var source = await db.Sources.FindAsync([id], cancellationToken: cancellationToken);

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
        var source = await db.Sources.FindAsync([id], cancellationToken: cancellationToken);

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
    public async Task<ActionResult<List<SourceSong>>> SearchSongsAsync(long id,
        string query, CancellationToken cancellationToken = default)
    {
        var source = await sourcesService.GetSourceClientAsync(id, cancellationToken);

        return await source.SearchSongsAsync(query, cancellationToken);
    }

    [HttpGet("{id}/songs/{songId}", Name = "Get Song")]
    public async Task<ActionResult<SourceSong>> GetSongAsync(long id, string songId,
        CancellationToken cancellationToken = default)
    {
        var source = await sourcesService.GetSourceClientAsync(id, cancellationToken);

        return await source.GetSongAsync(songId, cancellationToken);
    }

    [HttpGet("{id}/songs/purchase/{songId}", Name = "Purchase Song")]
    public async Task<ActionResult<Stream>> PurchaseSongAsync(long id, string songId,
        CancellationToken cancellationToken = default)
    {
        return NotFound();
    }

    #endregion Songs

    #region Albums

    [HttpGet("{id}/albums/search/{query}", Name = "Search Albums")]
    public async Task<ActionResult<List<SourceAlbum>>> SearchAlbumsAsync(long id,
        string query, CancellationToken cancellationToken = default)
    {
        return NotFound();
    }

    #endregion Albums

    #endregion Operations
}