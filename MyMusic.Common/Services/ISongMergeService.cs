namespace MyMusic.Common.Services;

public interface ISongMergeService
{
    /// <summary>
    ///     Merges <paramref name="mergeFromSongId"/> into <paramref name="keepSongId"/> and deletes it. Failures are
    ///     returned as a failed result, except when <paramref name="db"/> already has a transaction: then they are
    ///     thrown, for the transaction's owner to handle.
    /// </summary>
    Task<SongMergeResult> MergeSongsAsync(
        MusicDbContext db,
        long keepSongId,
        long mergeFromSongId,
        CancellationToken cancellationToken = default);
}

public record SongMergeResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static SongMergeResult Succeeded() => new() { Success = true };

    public static SongMergeResult Failed(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}
