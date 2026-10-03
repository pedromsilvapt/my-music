using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.NamingStrategies;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Resolves pending-action device paths for a <see cref="SongDevice"/>, applying naming-template
/// generation and unique-path collision handling.
/// </summary>
public interface ISyncPathResolver
{
    /// <summary>
    /// Computes the target device path (and previous path, if a rename is required) for the
    /// pending action against <paramref name="sd"/>, using <paramref name="namingStrategy"/> and
    /// consulting <paramref name="usedPaths"/> to avoid collisions with the device's other paths. The
    /// current path of <paramref name="sd"/> is never a collision, so a path with a collision counter is kept.
    /// </summary>
    (string Path, string? PreviousPath) ComputePendingActionPath(SongDevice sd, TemplateNamingStrategy namingStrategy, HashSet<string> usedPaths);

    /// <summary>
    /// Returns <paramref name="basePath"/> if unused by <paramref name="existingPaths"/>,
    /// otherwise returns a unique variant with a " (n)" suffix.
    /// </summary>
    string GetUniquePath(string basePath, HashSet<string> existingPaths);
}