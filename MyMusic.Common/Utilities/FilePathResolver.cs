using Microsoft.EntityFrameworkCore;
using MyMusic.Common;

namespace MyMusic.Common.Utilities;

/// <summary>
///     Resolves file path collisions by appending incrementing counter suffixes
///     to the filename when another song already occupies the target path.
/// </summary>
public static class FilePathResolver
{
    /// <summary>
    ///     Given a base file path, checks the database for existing songs owned by the same user
    ///     that have the same RepositoryPath. If a collision is found, appends a counter suffix
    ///     to the filename (before the extension) until a unique path is found.
    /// </summary>
    /// <param name="basePath">The desired file path (e.g., "Artist/Album/SongTitle.mp3")</param>
    /// <param name="userId">The owner's user ID, used to scope the collision check</param>
    /// <param name="db">The database context to query for existing paths</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The base path if no collision, or a path with a counter suffix if collision exists</returns>
    public static async Task<string> ResolveConflictAsync(
        string basePath,
        long userId,
        MusicDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (!await PathExistsAsync(db, userId, basePath, cancellationToken))
        {
            return basePath;
        }

        var directory = Path.GetDirectoryName(basePath) ?? "";
        var filenameWithoutExt = Path.GetFileNameWithoutExtension(basePath);
        var extension = Path.GetExtension(basePath);

        for (var counter = 2; ; counter++)
        {
            var candidateFilename = $"{filenameWithoutExt} ({counter}){extension}";
            var candidatePath = string.IsNullOrEmpty(directory)
                ? candidateFilename
                : Path.Combine(directory, candidateFilename);

            if (!await PathExistsAsync(db, userId, candidatePath, cancellationToken))
            {
                return candidatePath;
            }
        }
    }

    /// <summary>
    ///     Given a base file path, checks the database for existing songs owned by the same user
    ///     that have the same RepositoryPath, excluding a specific song (for relocation scenarios
    ///     where the song's own current path might match).
    /// </summary>
    /// <param name="basePath">The desired file path</param>
    /// <param name="userId">The owner's user ID</param>
    /// <param name="excludeSongId">A song ID to exclude from collision check (the song being relocated)</param>
    /// <param name="db">The database context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The base path if no collision, or a path with a counter suffix if collision exists</returns>
    public static async Task<string> ResolveConflictAsync(
        string basePath,
        long userId,
        long excludeSongId,
        MusicDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (!await PathExistsAsync(db, userId, basePath, excludeSongId, cancellationToken))
        {
            return basePath;
        }

        var directory = Path.GetDirectoryName(basePath) ?? "";
        var filenameWithoutExt = Path.GetFileNameWithoutExtension(basePath);
        var extension = Path.GetExtension(basePath);

        for (var counter = 2; ; counter++)
        {
            var candidateFilename = $"{filenameWithoutExt} ({counter}){extension}";
            var candidatePath = string.IsNullOrEmpty(directory)
                ? candidateFilename
                : Path.Combine(directory, candidateFilename);

            if (!await PathExistsAsync(db, userId, candidatePath, excludeSongId, cancellationToken))
            {
                return candidatePath;
            }
        }
    }

    private static async Task<bool> PathExistsAsync(
        MusicDbContext db, long userId, string path, CancellationToken cancellationToken)
    {
        return await db.Songs.AnyAsync(
            s => s.OwnerId == userId && s.RepositoryPath == path,
            cancellationToken);
    }

    private static async Task<bool> PathExistsAsync(
        MusicDbContext db, long userId, string path, long excludeSongId, CancellationToken cancellationToken)
    {
        return await db.Songs.AnyAsync(
            s => s.OwnerId == userId && s.RepositoryPath == path && s.Id != excludeSongId,
            cancellationToken);
    }
}