namespace MyMusic.Common.Services;

public interface ISongMergeService
{
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
