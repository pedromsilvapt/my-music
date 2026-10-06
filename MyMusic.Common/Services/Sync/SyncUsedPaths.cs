using MyMusic.Common.Entities;
using MyMusic.Common.NamingStrategies;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// The paths of a device that are taken during a sync session: the paths of its files, changed by the
/// records the session created so far. A path given to a file stays taken for the rest of the session.
/// </summary>
public class SyncUsedPaths(IEnumerable<string> paths)
{
    private readonly HashSet<string> _paths = [.. paths];

    /// <summary>
    /// Whether <paramref name="path"/> is taken.
    /// </summary>
    public bool Contains(string path) => _paths.Contains(path);

    /// <summary>
    /// Marks <paramref name="path"/> as taken.
    /// </summary>
    public void Reserve(string path) => _paths.Add(path);

    /// <summary>
    /// Marks <paramref name="path"/> as no longer taken.
    /// </summary>
    public void Free(string path) => _paths.Remove(path);

    /// <summary>
    /// Applies a rename: <paramref name="previousPath"/> is no longer taken, <paramref name="newPath"/> is.
    /// </summary>
    public void Rename(string previousPath, string newPath)
    {
        Free(previousPath);
        Reserve(newPath);
    }

    /// <summary>
    /// Computes the target path of the pending action against <paramref name="sd"/> (see
    /// <see cref="ISyncPathResolver.ComputePendingActionPath"/>) and marks it as taken. The previous path
    /// stays taken: the caller frees it with <see cref="Rename"/> when it creates the <c>Rename</c> record.
    /// </summary>
    public (string Path, string? PreviousPath) Take(ISyncPathResolver pathResolver, SongDevice sd, TemplateNamingStrategy namingStrategy)
    {
        var pendingAction = pathResolver.ComputePendingActionPath(sd, namingStrategy, _paths);
        Reserve(pendingAction.Path);

        return pendingAction;
    }
}
