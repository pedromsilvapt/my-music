using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Songs;

public interface ISongFileUpdateService
{
    /// <summary>
    ///     Writes the song's metadata into its file, and points the song to the path that metadata now generates. The
    ///     file itself is not moved there: the caller moves it from <see cref="SongFileUpdateResult.PreviousPath"/>
    ///     once that path is saved.
    ///     When the file's checksum changes, the song's devices are marked to download it again (with the given
    ///     reason), and its <see cref="Song.FileModifiedAt"/> is updated.
    /// </summary>
    /// <param name="song">Loaded with its owner, album (and its artist), artists, genres and cover.</param>
    Task<SongFileUpdateResult> UpdateAsync(MusicDbContext db, IFileTransaction files, Song song,
        Func<string> downloadReason, CancellationToken cancellationToken = default);
}

public record SongFileUpdateResult
{
    /// <summary>The path the file still has to be moved from, if its path changed.</summary>
    public required string? PreviousPath { get; init; }

    public required bool ChecksumChanged { get; init; }
}
