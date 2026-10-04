namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Loads the paths of a device that are taken during a sync session.
/// </summary>
public interface ISyncUsedPathsService
{
    /// <summary>
    /// Returns the used paths of the device in the session: the paths of its <c>SongDevice</c>s, changed
    /// by the session's records in the order they were created (a <c>Rename</c> frees its previous path
    /// and takes its new one, a <c>CreateLocal</c> takes its path, a <c>DeleteLocal</c> frees its path).
    /// Loaded once per request: later calls return the same instance, which the callers keep up to date
    /// with the records they create.
    /// </summary>
    Task<SyncUsedPaths> GetAsync(MusicDbContext db, long deviceId, long sessionId, CancellationToken cancellationToken);
}
