namespace MyMusic.CLI.Services.Dates;

/// <summary>
/// How many files of the device a date repair re-dated, and how many it left alone, per reason.
/// </summary>
public record RepairDatesResult
{
    /// <summary>Files given the configured date (or that would be, in a dry run).</summary>
    public int Redated { get; init; }

    /// <summary>Files with no song, marked for removal, or never synced.</summary>
    public int NotSynced { get; init; }

    /// <summary>Files whose path matches an exclusion rule.</summary>
    public int Excluded { get; init; }

    /// <summary>Files that are not on the device.</summary>
    public int Missing { get; init; }

    /// <summary>Files changed on the device since the last sync.</summary>
    public int EditedLocally { get; init; }

    /// <summary>Files whose song changed on the server since the last sync.</summary>
    public int ChangedOnServer { get; init; }

    /// <summary>Files that already have the configured date.</summary>
    public int UpToDate { get; init; }

    /// <summary>Files whose date could not be set.</summary>
    public int Failed { get; init; }

    public int Skipped => NotSynced + Excluded + Missing + EditedLocally + ChangedOnServer + UpToDate;
}

/// <summary>
/// Gives the files already on the device the date the device is configured to give the files a sync downloads
/// (see "Downloaded File Dates" in docs/development/sync.md), without running a sync.
/// </summary>
public interface IRepairDatesService
{
    /// <summary>
    /// Re-dates the synced files of the device. With <paramref name="dryRun"/>, no file is changed and the
    /// result counts what would be.
    /// </summary>
    Task<RepairDatesResult> RepairAsync(bool dryRun, CancellationToken ct = default);
}
