using MyMusic.Common.Services.SongHistory.Models;

namespace MyMusic.Common.Services.SongHistory;

public interface ISongHistorySnapshotService
{
    Task<SongSnapshot?> GetCurrentSnapshotAsync(long songId, bool includeCover, CancellationToken ct);

    /// <summary>
    /// Loads an artwork in the shape snapshots embed covers (base64 data), or <c>null</c> when it no longer exists.
    /// </summary>
    Task<SongSnapshotCover?> GetCoverAsync(long coverId, CancellationToken ct);
}
