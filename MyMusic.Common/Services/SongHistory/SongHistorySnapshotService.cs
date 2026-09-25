using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Services.SongHistory.Models;

namespace MyMusic.Common.Services.SongHistory;

public class SongHistorySnapshotService(MusicDbContext db) : ISongHistorySnapshotService
{
    public async Task<SongSnapshot?> GetCurrentSnapshotAsync(long songId, bool includeCover, CancellationToken ct)
    {
        var json = await db.Database
            .SqlQueryRaw<string>(
                "SELECT COALESCE(song_history_build_snapshot({0}, {1}, 'updated')::text, '') AS \"Value\"",
                songId,
                includeCover)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<SongSnapshot>(json, SongHistoryJsonOptions.Options);
    }
}