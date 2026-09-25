using MyMusic.Common.Services.SongHistory.Models;

namespace MyMusic.Common.Services.SongHistory;

public interface ISongHistoryDiffService
{
    SongHistoryDelta ComputeDiff(SongSnapshot oldSnapshot, SongSnapshot? newSnapshot);
}