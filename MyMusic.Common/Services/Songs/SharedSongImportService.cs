using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Models;

namespace MyMusic.Common.Services;

/// <summary>
/// <see cref="ISharedSongImportService"/> implementation. Stages a copy of the shared song's
/// bytes into a temp directory (safer than passing the owner's <see cref="Song.RepositoryPath"/>
/// directly — the owner could move/delete the file mid-import), then delegates to
/// <see cref="IMusicService.ImportRepositorySongs"/> with <see cref="DuplicateSongsHandlingStrategy.Skip"/>
/// so re-importing is idempotent.
/// </summary>
public class SharedSongImportService(
    IMusicService musicService,
    IFileSystem fileSystem,
    ILogger<MusicImportJob> importJobLogger,
    ILogger<SharedSongImportService> logger) : ISharedSongImportService
{
    /// <summary>
    /// Temp directory prefix matching the <c>SongsController.Upload</c> convention
    /// (<c>mymusic_upload_*</c>) so existing cleanup logic, if any, covers these too.
    /// </summary>
    public const string TempDirPrefix = "mymusic_shared_import_";

    public async Task<ImportSharedSongResult> ImportAsync(
        MusicDbContext db,
        long songId,
        long currentUserId,
        CancellationToken ct)
    {
        // Load the source song WITHOUT the ownership filter — it may be owned by another user.
        var song = await db.Songs.SingleOrDefaultAsync(s => s.Id == songId, ct);
        if (song == null)
        {
            throw new InvalidOperationException($"Song not found with id {songId}");
        }

        // Verify the song is actually shared with the current user. The sharer is derivable
        // from Song.OwnerId, so a SongSharing row keyed by (SongId, UserId) is the only proof.
        var isShared = await db.SongSharings.AnyAsync(ss => ss.SongId == songId && ss.UserId == currentUserId, ct);
        if (!isShared)
        {
            throw new UnauthorizedAccessException($"Song {songId} is not shared with user {currentUserId}");
        }

        // Stage the bytes into a temp directory we control. A copy is safer than passing the
        // owner's RepositoryPath directly: the owner could move/delete/re-tag the file while we
        // import, corrupting the recipient's import. Mirrors SongsController.Upload staging.
        var tempDir = fileSystem.Path.Combine(fileSystem.Path.GetTempPath(), $"{TempDirPrefix}{Guid.NewGuid()}");
        fileSystem.Directory.CreateDirectory(tempDir);

        try
        {
            string tempFilePath;
            try
            {
                var sourceFileName = fileSystem.Path.GetFileName(song.RepositoryPath);
                tempFilePath = fileSystem.Path.Combine(tempDir, sourceFileName);

                await using (var sourceStream = fileSystem.File.OpenRead(song.RepositoryPath))
                await using (var destStream = fileSystem.FileStream.New(tempFilePath, FileMode.Create))
                {
                    await sourceStream.CopyToAsync(destStream, ct);
                }
            }
            catch (Exception ex)
            {
                // Staging failed (owner moved/deleted the file mid-import, I/O error, etc.).
                // Surface a structured failure rather than throwing so the controller can map to a 200
                // with an error body, mirroring SongsController.Upload's resilience.
                logger.LogError(ex, "Failed to stage shared song bytes for SongId={SongId}", songId);
                return ImportSharedSongResult.Fail($"Failed to read source file: {ex.Message}");
            }

            // Preserve the source's timestamps so the recipient's copy carries the same
            // origin metadata. Fall back to a sensible UTC now when the owner's row lacks them.
            var createdAt = song.CreatedAt == default ? DateTime.UtcNow : song.CreatedAt;
            var modifiedAt = song.FileModifiedAt ?? song.ModifiedAt;

            var meta = new SongImportMetadata(tempFilePath, createdAt, modifiedAt);
            var job = new MusicImportJob(importJobLogger);

            await musicService.ImportRepositorySongs(
                db,
                job,
                currentUserId,
                new[] { meta },
                deviceIds: null,
                DuplicateSongsHandlingStrategy.Skip,
                ct);

            var importedSong = job.SongMapping.Values.FirstOrDefault();

            if (importedSong == null)
            {
                // Mirror SongsController.Upload error formatting: prefer a skip reason, then an exception.
                var skipReason = job.SkipReasons.FirstOrDefault(s => s.SourceFilePath == tempFilePath);
                var exception = job.Exceptions.FirstOrDefault();

                var errorParts = new List<string>();
                if (skipReason != null)
                {
                    errorParts.Add(string.Format(skipReason.Message, skipReason.MessageArgs));
                }
                if (exception != null)
                {
                    errorParts.Add($"Exception: {exception.Message}");
                }
                if (skipReason == null && exception == null)
                {
                    errorParts.Add("No song was imported and no skip reason was recorded");
                }

                var error = string.Join("; ", errorParts);
                logger.LogError("Shared song import failed for SongId={SongId}, UserId={UserId}: {Error}", songId, currentUserId, error);
                return ImportSharedSongResult.Fail(error);
            }

            // Skip strategy records a DuplicateChecksumSkipReason when the recipient already owned a
            // copy with the same checksum AND that file still exists on disk — that is our "already imported" signal.
            var alreadyImported = job.SkipReasons.Any();
            return ImportSharedSongResult.Ok(importedSong.Id, alreadyImported);
        }
        finally
        {
            if (fileSystem.Directory.Exists(tempDir))
            {
                try
                {
                    fileSystem.Directory.Delete(tempDir, recursive: true);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete temp import directory {TempDir}", tempDir);
                }
            }
        }
    }
}