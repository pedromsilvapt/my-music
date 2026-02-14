using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.DTO.Purchases;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("purchases")]
public class PurchasesController(
    ILogger<PurchasesController> logger,
    ICurrentUser currentUser,
    ISourcesService sourcesService) : ControllerBase
{
    private readonly ILogger<PurchasesController> _logger = logger;

    [HttpGet(Name = "ListPurchases")]
    public async Task<ListPurchasesResponse> List(MusicDbContext context, CancellationToken cancellationToken)
    {
        var purchases = await context.PurchasedSongs
            .Where(s => s.UserId == currentUser.Id)
            .OrderByDescending(s => s.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new ListPurchasesResponse
        {
            Purchases = purchases.Select(ListPurchasesItem.FromEntity).ToList(),
        };
    }

    [HttpPost("create/{sourceId}/{songId}", Name = "CreatePurchase")]
    public async Task<CreatePurchaseResponse> Create(
        [FromServices] MusicDbContext db,
        [FromServices] PurchasesQueue purchasesQueue,
        [FromRoute] long sourceId,
        [FromRoute] string songId,
        CancellationToken cancellationToken)
    {
        await using var dbTrans = await db.Database.BeginTransactionAsync(cancellationToken);

        var source = await sourcesService.GetSourceClientAsync(sourceId, cancellationToken);

        var sourceSong = await source.GetSongAsync(songId, cancellationToken);

        var artists = string.Join(", ", sourceSong.Artists.Select(a => a.Name));
        var album = sourceSong.Album.Name;

        var purchasedSong = new PurchasedSong
        {
            ExternalId = songId,
            CreatedAt = DateTime.UtcNow,
            SourceId = sourceId,
            Cover = sourceSong.Cover?.Normal ?? sourceSong.Cover?.Smallest,
            Title = sourceSong.Title,
            SubTitle = sourceSong.Year != null
                ? $"{artists} • {album} • {sourceSong.Year.Value}"
                : $"{artists} • {album}",
            Status = PurchasedSongStatus.Queued,
            Progress = 0,
            UserId = currentUser.Id,
        };

        await db.PurchasedSongs.AddAsync(purchasedSong, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await dbTrans.CommitAsync(cancellationToken);

        await purchasesQueue.Scheduler.TryScheduleTasksAsync();

        return new CreatePurchaseResponse
        {
            Purchase = CreatePurchaseItem.FromEntity(purchasedSong),
        };
    }

    [HttpPost("requeue", Name = "RequeueManyPurchases")]
    public async Task<ActionResult<RequeueManyPurchasesResponse>> RequeueMany(
        [FromServices] MusicDbContext db,
        [FromServices] PurchasesQueue purchasesQueue,
        [FromQuery] long[] ids,
        CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return BadRequest();
        }

        var purchases = await db.PurchasedSongs
            .Where(x => ids.Contains(x.Id) && x.UserId == currentUser.Id)
            .ToListAsync(cancellationToken);

        if (purchases.Count != ids.Length)
        {
            return NotFound();
        }

        if (purchases.Any(purchase => purchase.Status != PurchasedSongStatus.Failed))
        {
            return BadRequest();
        }

        foreach (var purchase in purchases)
        {
            purchase.Status = PurchasedSongStatus.Queued;
        }

        db.Update(purchases);
        await db.SaveChangesAsync(cancellationToken);

        await purchasesQueue.Scheduler.TryScheduleTasksAsync();

        return new RequeueManyPurchasesResponse
        {
            Purchases = purchases.Select(RequeueManyPurchasesItem.FromEntity).ToList(),
        };
    }


    [HttpPost("{id}/requeue", Name = "RequeuePurchase")]
    public async Task<ActionResult<RequeuePurchaseResponse>> RequeueMany(
        [FromServices] MusicDbContext db,
        [FromServices] PurchasesQueue purchasesQueue,
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var purchase = await db.PurchasedSongs
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == currentUser.Id, cancellationToken);

        if (purchase == null)
        {
            return NotFound();
        }

        if (purchase.Status != PurchasedSongStatus.Failed)
        {
            return BadRequest();
        }

        purchase.Status = PurchasedSongStatus.Queued;

        db.Update(purchase);
        await db.SaveChangesAsync(cancellationToken);

        await purchasesQueue.Scheduler.TryScheduleTasksAsync();

        return new RequeuePurchaseResponse
        {
            Purchase = RequeuePurchaseItem.FromEntity(purchase),
        };
    }

    [HttpDelete(Name = "DeleteManyPurchases")]
    public async Task<ActionResult<DeleteManyPurchasesResponse>> DeleteMany(
        [FromServices] MusicDbContext db,
        [FromQuery] bool onlyFinished,
        [FromQuery] long[]? ids = null,
        CancellationToken cancellationToken = default)
    {
        PurchasedSongStatus[] status = onlyFinished
            ? [PurchasedSongStatus.Completed, PurchasedSongStatus.Failed]
            : [PurchasedSongStatus.Completed, PurchasedSongStatus.Failed, PurchasedSongStatus.Queued];

        var query = db.PurchasedSongs
            .Where(x => x.UserId == currentUser.Id &&
                        status.Contains(x.Status));

        if (ids is { Length: > 0 })
        {
            query = query.Where(x => ids.Contains(x.Id));
        }

        var purchases = await query.ToListAsync(cancellationToken);

        if (purchases.Count > 0)
        {
            foreach (var purchase in purchases)
            {
                db.Remove(purchase);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return new DeleteManyPurchasesResponse
        {
            Purchases = purchases.Select(DeleteManyPurchasesItem.FromEntity).ToList(),
        };
    }

    [HttpDelete("{id}", Name = "DeletePurchase")]
    public async Task<ActionResult<DeletePurchaseResponse>> Delete(
        [FromServices] MusicDbContext db,
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var purchase =
            await db.PurchasedSongs.FirstOrDefaultAsync(x => x.Id == id && x.UserId == currentUser.Id,
                cancellationToken);

        if (purchase is null)
        {
            return NotFound();
        }

        if (purchase.Status == PurchasedSongStatus.Acquiring)
        {
            return BadRequest();
        }

        db.Remove(purchase);
        await db.SaveChangesAsync(cancellationToken);

        return new DeletePurchaseResponse
        {
            Purchase = DeletePurchaseItem.FromEntity(purchase),
        };
    }
}