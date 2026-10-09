using Microsoft.Extensions.Logging;
using MyMusic.CLI.Api;
using MyMusic.CLI.Api.Dtos;
using MyMusic.CLI.Services.Sync;
using MyMusic.CLI.Services.Sync.Types;

namespace MyMusic.CLI.Services.Dates;

/// <summary>
/// Default implementation of <see cref="IRepairDatesService"/>.
/// </summary>
public class RepairDatesService(
    ISyncConfig config,
    IMyMusicClient client,
    IFileOps fileOps,
    ILogger<RepairDatesService> logger) : IRepairDatesService
{
    private enum Outcome
    {
        Redated,
        NotSynced,
        Excluded,
        Missing,
        EditedLocally,
        ChangedOnServer,
        UpToDate,
        Failed,
    }

    /// <inheritdoc />
    public async Task<RepairDatesResult> RepairAsync(bool dryRun, CancellationToken ct = default)
    {
        var deviceId = await config.GetDeviceIdAsync(ct)
            ?? throw new InvalidOperationException("No device is configured");
        var repositoryPath = config.GetRepositoryPath();
        var source = config.GetFileModifiedAt();
        var exclusions = new ExclusionMatcher(config.GetExcludePatterns());

        var response = await client.GetDeviceSongsAsync(deviceId, ct);

        var counts = new Dictionary<Outcome, int>();
        foreach (var song in response.Songs)
        {
            ct.ThrowIfCancellationRequested();

            var outcome = await RepairFileAsync(song, repositoryPath, source, exclusions, dryRun, ct);
            counts[outcome] = counts.GetValueOrDefault(outcome) + 1;
        }

        return new RepairDatesResult
        {
            Redated = counts.GetValueOrDefault(Outcome.Redated),
            NotSynced = counts.GetValueOrDefault(Outcome.NotSynced),
            Excluded = counts.GetValueOrDefault(Outcome.Excluded),
            Missing = counts.GetValueOrDefault(Outcome.Missing),
            EditedLocally = counts.GetValueOrDefault(Outcome.EditedLocally),
            ChangedOnServer = counts.GetValueOrDefault(Outcome.ChangedOnServer),
            UpToDate = counts.GetValueOrDefault(Outcome.UpToDate),
            Failed = counts.GetValueOrDefault(Outcome.Failed),
        };
    }

    private async Task<Outcome> RepairFileAsync(DeviceSongItem song, string repositoryPath, FileModifiedAtSource source,
        ExclusionMatcher exclusions, bool dryRun, CancellationToken ct)
    {
        if (song.SongId == null || song.Action == "Remove" || song.LastSyncedModifiedAt is not { } lastSyncedValue)
        {
            logger.LogDebug("Skipping {Path}: not synced yet, or marked for removal", song.Path);
            return Outcome.NotSynced;
        }

        if (exclusions.Match(song.Path) is { } rule)
        {
            logger.LogDebug("Skipping {Path}: excluded by the rule '{Rule}'", song.Path, rule);
            return Outcome.Excluded;
        }

        var fullPath = Path.Combine(repositoryPath, song.Path);
        if (!fileOps.FileExists(fullPath) || await fileOps.GetModificationTimeAsync(fullPath, ct) is not { } fileDateValue)
        {
            logger.LogDebug("Skipping {Path}: not on the device", song.Path);
            return Outcome.Missing;
        }

        var lastSynced = lastSyncedValue.ToUniversalTime();
        var fileDate = fileDateValue.ToUniversalTime();

        // Re-dating a file edited on the device would hide the edit from the next sync
        if (SyncDateComparison.IsNewerThan(fileDate, lastSynced))
        {
            logger.LogDebug("Skipping {Path}: changed on the device since the last sync", song.Path);
            return Outcome.EditedLocally;
        }

        var targetValue = source switch
        {
            FileModifiedAtSource.ServerModifiedAt => song.ServerModifiedAt,
            FileModifiedAtSource.ServerCreatedAt => song.ServerCreatedAt,
            // The time the sync wrote the file
            _ => lastSynced,
        };
        if (targetValue is not { } target)
        {
            logger.LogDebug("Skipping {Path}: the server sent no date for it", song.Path);
            return Outcome.NotSynced;
        }

        target = target.ToUniversalTime();

        // The next sync downloads the song, and dates the file then. A date after the last sync would also make
        // the file look changed on the device
        if (SyncDateComparison.IsNewerThan(target, lastSynced))
        {
            logger.LogDebug("Skipping {Path}: the song changed on the server since the last sync", song.Path);
            return Outcome.ChangedOnServer;
        }

        if (!SyncDateComparison.IsNewerThan(fileDate, target) && !SyncDateComparison.IsNewerThan(target, fileDate))
        {
            logger.LogDebug("Skipping {Path}: already dated {Date:O}", song.Path, target);
            return Outcome.UpToDate;
        }

        if (dryRun)
        {
            logger.LogInformation("Would re-date {Path} from {From:O} to {To:O}", song.Path, fileDate, target);
            return Outcome.Redated;
        }

        try
        {
            await fileOps.SetModificationTimeAsync(fullPath, target, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to set the modified date of {Path}", song.Path);
            return Outcome.Failed;
        }

        logger.LogInformation("Re-dated {Path} from {From:O} to {To:O}", song.Path, fileDate, target);
        return Outcome.Redated;
    }
}
