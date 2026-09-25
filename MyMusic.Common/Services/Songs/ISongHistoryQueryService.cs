using MyMusic.Common.Entities;
using SongHistoryEntity = MyMusic.Common.Entities.SongHistory;

namespace MyMusic.Common.Services.Songs;

public interface ISongHistoryQueryService
{
    /// <summary>
    /// Returns the revision history for a song, ordered by <see cref="SongHistoryEntity.SongRevision"/>
    /// ascending. Only returns history when the song exists and belongs to the current user;
    /// returns an empty list otherwise (including when the song has been deleted).
    /// </summary>
    Task<List<SongHistoryEntity>> GetSongHistoryAsync(long songId, CancellationToken cancellationToken = default);
}