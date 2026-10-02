namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Default implementation of <see cref="ISyncDeduplicatePrepareService"/>.
/// </summary>
public class SyncDeduplicatePrepareService(
    MusicDbContext db,
    ISyncSessionLookupService sessionLookup,
    ISyncSoundalikeMatcher soundalikeMatcher) : ISyncDeduplicatePrepareService
{
    /// <summary>Library songs fingerprinted per call: small enough for frequent progress updates.</summary>
    public const int BatchSize = 20;

    /// <inheritdoc />
    public async Task<SyncLibraryPreparation?> PrepareAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        CancellationToken cancellationToken)
    {
        var activeSessionResult = await sessionLookup.GetActiveSessionAsync(db, sessionId, deviceId, ownerId, cancellationToken);
        if (!activeSessionResult.Found)
        {
            if (activeSessionResult.Failure == ActiveSessionFailure.NotFound) return null;
            throw new SyncDeduplicatePrepareValidationException(
                $"Sync session {sessionId} is not in progress (status: {activeSessionResult.NotInProgressStatus})");
        }

        if (!activeSessionResult.Session!.Deduplicate)
        {
            throw new SyncDeduplicatePrepareValidationException(
                $"Sync session {sessionId} was not started with deduplication");
        }

        return await soundalikeMatcher.PrepareLibraryAsync(sessionId, ownerId, BatchSize, cancellationToken);
    }
}
