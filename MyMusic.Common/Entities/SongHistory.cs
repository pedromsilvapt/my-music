using MyMusic.Common.Services.SongHistory.Models;

namespace MyMusic.Common.Entities;

public class SongHistory
{
    public long Id { get; set; }

    public long SongId { get; set; }

    public int SongRevision { get; set; }

    public required SongHistoryDelta Diff { get; set; }

    public string DiffFormat { get; set; } = "delta";

    public DateTime CreatedAt { get; set; }
}