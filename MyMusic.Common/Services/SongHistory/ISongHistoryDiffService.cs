using MyMusic.Common.Services.SongHistory.Models;

namespace MyMusic.Common.Services.SongHistory;

public interface ISongHistoryDiffService
{
    SongHistoryDelta ComputeDiff(SongSnapshot oldSnapshot, SongSnapshot? newSnapshot);

    /// <summary>
    /// Builds the <c>created</c> baseline delta of a song: every tracked field of <paramref name="snapshot"/> as a
    /// change with no previous value, so the baseline shows the song's full state rather than only what differs
    /// from a default.
    /// </summary>
    SongHistoryDelta ComputeBaseline(SongSnapshot snapshot);
}
