using MyMusic.Common.Services.SongHistory.Models;

namespace MyMusic.Common.Services.SongHistory;

public interface ISongHistorySnapshotService
{
    Task<SongSnapshot?> GetCurrentSnapshotAsync(long songId, bool includeCover, CancellationToken ct);
}