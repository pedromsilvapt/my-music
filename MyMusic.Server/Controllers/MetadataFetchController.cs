using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Sources;
using MyMusic.Server.DTO.MetadataFetch;
using MyMusic.Server.DTO.Songs;
using MyMusic.Server.Mapping;

namespace MyMusic.Server.Controllers;

/// <summary>
/// Controller for metadata auto-fetch operations.
/// </summary>
[ApiController]
[Route("metadata-fetch")]
public class MetadataFetchController(
    ILogger<MetadataFetchController> logger,
    MetadataFetchQueue metadataFetchQueue,
    MetadataDiffBuilder metadataDiffBuilder,
    IThumbnailProxyService thumbnailProxyService) : ControllerBase
{
    /// <summary>
    /// Triggers background tasks to fetch metadata for all eligible songs.
    /// Rate limited: 1 request per minute per user.
    /// </summary>
    [HttpPost("batch")]
    [EnableRateLimiting("MetadataFetchBatch")]
    public async Task<ActionResult<BatchMetadataFetchResponse>> TriggerBatchFetch(
        [FromServices] MusicDbContext db,
        [FromServices] ICurrentUser currentUser,
        [FromBody] BatchMetadataFetchRequest? request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[AUDIT] User {UserId} triggered batch metadata fetch",
            currentUser.Id);

        // Build base query for songs with non-waived audit issues
        var eligibleSongsQuery = db.Songs
            .AsNoTracking()
            .Where(s => db.AuditNonConformities
                .Any(nc => nc.SongId == s.Id && !nc.HasWaiver && nc.OwnerId == currentUser.Id));

        // If specific song IDs provided, filter to those
        if (request?.SongIds?.Count > 0)
        {
            eligibleSongsQuery = eligibleSongsQuery.Where(s => request.SongIds.Contains(s.Id));
        }

        // Exclude songs with recent auto-fetched metadata (within 30 days)
        // Also exclude songs that already have queued or processing tasks
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        var eligibleSongs = await eligibleSongsQuery
            .Where(s => !db.AutoFetchedMetadata
                .Any(afm => afm.SongId == s.Id
                    && afm.Status != AutoFetchStatus.Failed
                    && afm.FetchedAt > thirtyDaysAgo))
            .Where(s => !db.MetadataFetchTasks
                .Any(t => t.SongId == s.Id
                    && (t.Status == MetadataFetchStatus.Queued || t.Status == MetadataFetchStatus.Processing)))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (eligibleSongs.Count == 0)
        {
            logger.LogInformation("No eligible songs found for metadata fetch");

            return Ok(new BatchMetadataFetchResponse
            {
                TasksCreated = 0,
                Message = "No songs need metadata fetching at this time."
            });
        }

        // Create tasks for each eligible song
        var tasks = new List<MetadataFetchTask>();
        var now = DateTime.UtcNow;

        foreach (var songId in eligibleSongs)
        {
            var task = new MetadataFetchTask
            {
                SongId = songId,
                Status = MetadataFetchStatus.Queued,
                Progress = 0,
                CreatedAt = now
            };

            tasks.Add(task);
        }

        await db.MetadataFetchTasks.AddRangeAsync(tasks, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // Trigger the scheduler to pick up new tasks
        await metadataFetchQueue.Scheduler.TryScheduleTasksAsync();

        logger.LogInformation(
            "[AUDIT] Created {Count} metadata fetch tasks for user {UserId}",
            tasks.Count,
            currentUser.Id);

        return Ok(new BatchMetadataFetchResponse
        {
            TasksCreated = tasks.Count,
            Message = $"{tasks.Count} song(s) queued for metadata fetching."
        });
    }

    /// <summary>
    /// Gets pending auto-fetched metadata for a specific song.
    /// Constructs the metadata diff at runtime from stored raw source data.
    /// </summary>
    /// <param name="db">Database context</param>
    /// <param name="currentUser">Current authenticated user</param>
    /// <param name="fieldMapper">Audit rule field mapper</param>
    /// <param name="songId">Song ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Auto-fetched metadata if available</returns>
    [HttpGet("song/{songId:long}")]
    public async Task<ActionResult<AutoFetchedMetadataResponse>> GetAutoFetchedMetadata(
        [FromServices] MusicDbContext db,
        [FromServices] ICurrentUser currentUser,
        [FromServices] IAuditRuleFieldMapper fieldMapper,
        [FromRoute] long songId,
        CancellationToken cancellationToken)
    {
        // Load song with all related entities needed for diff construction
        var song = await db.Songs
            .AsNoTracking()
            .Include(s => s.Artists)
            .ThenInclude(sa => sa.Artist)
            .Include(s => s.Genres)
            .ThenInclude(sg => sg.Genre)
            .Include(s => s.Cover)
            .Include(s => s.Album)
            .ThenInclude(a => a!.Artist)
            .FirstOrDefaultAsync(s => s.Id == songId && s.OwnerId == currentUser.Id, cancellationToken);

        if (song == null)
        {
            return NotFound("Song not found");
        }

        // Get the most recent pending metadata
        var metadata = await db.AutoFetchedMetadata
            .AsNoTracking()
            .Include(m => m.Source)
            .Where(m => m.SongId == songId && m.Status == AutoFetchStatus.Pending)
            .OrderByDescending(m => m.FetchedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (metadata == null)
        {
            return Ok(new AutoFetchedMetadataResponse
            {
                HasMetadata = false
            });
        }

        // Get audit rules for this song to determine pre-selected fields
        var auditRuleIds = await db.AuditNonConformities
            .AsNoTracking()
            .Where(nc => nc.SongId == songId && !nc.HasWaiver)
            .Select(nc => nc.AuditRuleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var preSelectedFields = fieldMapper.GetFieldsForRules(auditRuleIds);

        // Deserialize the raw source metadata and construct diff at runtime
        SongMetadataDiff? diff = null;
        try
        {
            var sourceSong = JsonSerializer.Deserialize<SourceSong>(metadata.SourceMetadata);
            if (sourceSong != null)
            {
                // Build diff at runtime using the shared builder
                var diffModel = await metadataDiffBuilder.CreateDiffAsync(song, sourceSong, cancellationToken);
                diff = MetadataDiffMapper.ToSongMetadataDiff(diffModel);
                
                // Apply thumbnail proxy to the new cover URL (same as manual fetch)
                if (diff?.Cover is not null)
                {
                    diff.Cover = new SongMetadataField<string>
                    {
                        Old = diff.Cover.Old,
                        New = thumbnailProxyService.GetProxyUrl(diff.Cover.New),
                    };
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to construct metadata diff for song {SongId}", songId);
        }

        return Ok(new AutoFetchedMetadataResponse
        {
            HasMetadata = true,
            Metadata = diff,
            FetchedAt = metadata.FetchedAt,
            SourceName = metadata.Source?.Name,
            PreSelectedFields = preSelectedFields
        });
    }

    /// <summary>
    /// Marks auto-fetched metadata as applied after user saves changes.
    /// </summary>
    /// <param name="db">Database context</param>
    /// <param name="currentUser">Current authenticated user</param>
    /// <param name="songId">Song ID</param>
    /// <param name="request">Request with optional applied fields</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success response</returns>
    [HttpPost("song/{songId:long}/apply")]
    public async Task<ActionResult<ApplyMetadataResponse>> ApplyMetadata(
        [FromServices] MusicDbContext db,
        [FromServices] ICurrentUser currentUser,
        [FromRoute] long songId,
        [FromBody] ApplyMetadataRequest? request,
        CancellationToken cancellationToken)
    {
        // Verify the song belongs to the current user
        var song = await db.Songs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == songId && s.OwnerId == currentUser.Id, cancellationToken);

        if (song == null)
        {
            return NotFound("Song not found");
        }

        // Find the most recent pending metadata
        var metadata = await db.AutoFetchedMetadata
            .Where(m => m.SongId == songId && m.Status == AutoFetchStatus.Pending)
            .OrderByDescending(m => m.FetchedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (metadata == null)
        {
            return Ok(new ApplyMetadataResponse
            {
                Success = true,
                Message = "No pending metadata to apply"
            });
        }

        // Update status to Applied
        metadata.Status = AutoFetchStatus.Applied;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[AUDIT] User {UserId} applied auto-fetched metadata for song {SongId}",
            currentUser.Id,
            songId);

        return Ok(new ApplyMetadataResponse
        {
            Success = true,
            Message = "Metadata marked as applied"
        });
    }

    /// <summary>
    /// Gets the current status of the metadata fetch queue.
    /// </summary>
    /// <param name="db">Database context</param>
    /// <param name="currentUser">Current authenticated user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Queue status counts</returns>
    [HttpGet("queue-status")]
    public async Task<ActionResult<MetadataQueueStatusResponse>> GetQueueStatus(
        [FromServices] MusicDbContext db,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        // Get counts by status
        var queued = await db.MetadataFetchTasks
            .AsNoTracking()
            .CountAsync(t => t.Status == MetadataFetchStatus.Queued, cancellationToken);

        var processing = await db.MetadataFetchTasks
            .AsNoTracking()
            .CountAsync(t => t.Status == MetadataFetchStatus.Processing, cancellationToken);

        var completed = await db.MetadataFetchTasks
            .AsNoTracking()
            .CountAsync(t => t.Status == MetadataFetchStatus.Completed, cancellationToken);

        var failed = await db.MetadataFetchTasks
            .AsNoTracking()
            .CountAsync(t => t.Status == MetadataFetchStatus.Failed, cancellationToken);

        var total = queued + processing + completed + failed;

        // Estimate completion time (rough estimate: 30 seconds per remaining task)
        var remaining = queued + processing;
        var estimatedCompletion = remaining > 0
            ? DateTime.UtcNow.AddSeconds(remaining * 30)
            : (DateTime?)null;

        return Ok(new MetadataQueueStatusResponse
        {
            Queued = queued,
            Processing = processing,
            Completed = completed,
            Failed = failed,
            Total = total,
            EstimatedCompletion = estimatedCompletion
        });
    }

    /// <summary>
    /// Requeues failed metadata fetch tasks for retry.
    /// </summary>
    /// <param name="db">Database context</param>
    /// <param name="currentUser">Current authenticated user</param>
    /// <param name="request">Request with optional task IDs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of requeue operation</returns>
    [HttpPost("requeue")]
    public async Task<ActionResult<RequeueFailedMetadataResponse>> RequeueFailed(
        [FromServices] MusicDbContext db,
        [FromServices] ICurrentUser currentUser,
        [FromBody] RequeueFailedMetadataRequest? request,
        CancellationToken cancellationToken)
    {
        // Build query for failed tasks
        var query = db.MetadataFetchTasks
            .Where(t => t.Status == MetadataFetchStatus.Failed);

        // If specific IDs provided, filter to those
        if (request?.TaskIds?.Count > 0)
        {
            query = query.Where(t => request.TaskIds.Contains(t.Id));
        }

        var failedTasks = await query.ToListAsync(cancellationToken);

        if (failedTasks.Count == 0)
        {
            return Ok(new RequeueFailedMetadataResponse
            {
                RequeuedCount = 0,
                FailedCount = 0
            });
        }

        var requeuedCount = 0;
        var failedCount = 0;

        foreach (var task in failedTasks)
        {
            try
            {
                task.Status = MetadataFetchStatus.Queued;
                task.ErrorMessage = null;
                task.Progress = 0;
                task.StartedAt = null;
                task.CompletedAt = null;
                requeuedCount++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to requeue task {TaskId}", task.Id);
                failedCount++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // Trigger the scheduler to pick up requeued tasks
        await metadataFetchQueue.Scheduler.TryScheduleTasksAsync();

        logger.LogInformation(
            "[AUDIT] User {UserId} requeued {RequeuedCount} failed metadata fetch tasks",
            currentUser.Id,
            requeuedCount);

        return Ok(new RequeueFailedMetadataResponse
        {
            RequeuedCount = requeuedCount,
            FailedCount = failedCount
        });
    }

    /// <summary>
    /// Gets detailed information about all failed metadata fetch tasks.
    /// </summary>
    /// <param name="db">Database context</param>
    /// <param name="currentUser">Current authenticated user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of failed tasks with details</returns>
    [HttpGet("failed-tasks")]
    public async Task<ActionResult<List<FailedTaskDetailResponse>>> GetFailedTasks(
        [FromServices] MusicDbContext db,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        // Get failed tasks with song information
        var failedTasks = await db.MetadataFetchTasks
            .AsNoTracking()
            .Where(t => t.Status == MetadataFetchStatus.Failed)
            .Include(t => t.Song)
            .OrderByDescending(t => t.CompletedAt)
            .Select(t => new FailedTaskDetailResponse
            {
                TaskId = t.Id,
                SongId = t.SongId,
                SongTitle = t.Song != null ? t.Song.Title : $"Song #{t.SongId}",
                Reason = t.FailureReason,
                ErrorMessage = t.ErrorMessage ?? "Unknown error",
                FailedAt = t.CompletedAt ?? t.CreatedAt,
                RetryCount = 0 // Future enhancement: track retry count
            })
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "[AUDIT] User {UserId} retrieved {Count} failed metadata fetch tasks",
            currentUser.Id,
            failedTasks.Count);

        return Ok(failedTasks);
    }

    /// <summary>
    /// Clears all metadata fetch tasks and auto-fetched metadata.
    /// This permanently deletes all task queue entries and pending/applied metadata.
    /// </summary>
    /// <param name="db">Database context</param>
    /// <param name="currentUser">Current authenticated user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Counts of deleted records</returns>
    [HttpPost("clear-all")]
    public async Task<ActionResult<ClearAllTasksResponse>> ClearAllTasksAndMetadata(
        [FromServices] MusicDbContext db,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        // Delete all metadata fetch tasks (queued, processing, completed, failed)
        var tasksDeleted = await db.MetadataFetchTasks
            .ExecuteDeleteAsync(cancellationToken);

        // Delete all auto-fetched metadata (pending, applied, failed, expired)
        var metadataDeleted = await db.AutoFetchedMetadata
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation(
            "[AUDIT] User {UserId} cleared all tasks and metadata: {TasksDeleted} tasks, {MetadataDeleted} metadata records",
            currentUser.Id,
            tasksDeleted,
            metadataDeleted);

        return Ok(new ClearAllTasksResponse
        {
            TasksDeleted = tasksDeleted,
            MetadataDeleted = metadataDeleted
        });
    }
}
