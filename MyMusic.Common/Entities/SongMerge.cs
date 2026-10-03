namespace MyMusic.Common.Entities;

/// <summary>
/// Records that one song absorbed another. Rows are never changed or copied: when the kept song is itself merged
/// into another song later, a new row is added for it, so the rows form a tree of merges.
/// </summary>
public class SongMerge
{
    public long Id { get; set; }

    /// <summary>The song that absorbed the other one.</summary>
    public long KeptSongId { get; set; }

    /// <summary>The song that was merged away, and no longer exists.</summary>
    public long MergedSongId { get; set; }

    public User Owner { get; set; } = null!;
    public long OwnerId { get; set; }

    public SongMergeKind Kind { get; set; }

    public DateTime MergedAt { get; set; }
}

public enum SongMergeKind
{
    /// <summary>An imported file turned out to be the same song as an existing one.</summary>
    ImportDuplicate,

    /// <summary>A soundalike resolved with the "Merge" action: its metadata was merged into the kept song.</summary>
    SoundalikeMerge,

    /// <summary>A soundalike resolved with the "Delete" action: it was discarded in favour of the kept song.</summary>
    SoundalikeDelete,
}
