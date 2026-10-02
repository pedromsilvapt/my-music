using System.ComponentModel.DataAnnotations;
using MyMusic.Common.Services.SongHistory.Models;

namespace MyMusic.Common.Entities;

public class SongHistory
{
    /// <summary>The revision recording a song's baseline state, with no previous values to compare against.</summary>
    public const string CreatedAction = "created";

    public const string UpdatedAction = "updated";

    public long Id { get; set; }

    public long SongId { get; set; }

    public int SongRevision { get; set; }

    public required SongHistoryDelta Diff { get; set; }

    public string DiffFormat { get; set; } = "delta";

    /// <summary>
    /// Mirrors <see cref="SongHistoryDelta.Action"/> (<c>created</c>, <c>updated</c> or <c>deleted</c>), as a column so
    /// the songs still missing their <see cref="CreatedAction"/> baseline can be found without parsing the diff.
    /// </summary>
    [MaxLength(16)]
    public string Action { get; set; } = UpdatedAction;

    public DateTime CreatedAt { get; set; }
}
