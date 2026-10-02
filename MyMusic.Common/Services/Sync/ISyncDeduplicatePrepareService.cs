namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Thrown when a sync session cannot prepare deduplication: it is not in progress, or was not started with
/// <c>Deduplicate</c>. The controller maps it to <c>400 Bad Request</c>.
/// </summary>
public class SyncDeduplicatePrepareValidationException(string message) : Exception(message);

/// <summary>
/// Fingerprints the library of a sync session started with <c>Deduplicate</c> ahead of its uploads, one
/// batch per call, so the client can show progress instead of stalling on its first upload. See
/// docs/development/sync.md, "Soundalike Deduplication".
/// </summary>
public interface ISyncDeduplicatePrepareService
{
    /// <summary>
    /// Fingerprints the next batch of library songs of the session's owner that have no stored fingerprint.
    /// Returns <c>null</c> when no such session exists for the device and owner.
    /// </summary>
    Task<SyncLibraryPreparation?> PrepareAsync(
        long deviceId,
        long sessionId,
        long ownerId,
        CancellationToken cancellationToken);
}
