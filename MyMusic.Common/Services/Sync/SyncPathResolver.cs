using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.NamingStrategies;

namespace MyMusic.Common.Services.Sync;

/// <summary>
/// Default implementation of <see cref="ISyncPathResolver"/>.
/// </summary>
public class SyncPathResolver : ISyncPathResolver
{
    /// <inheritdoc />
    public (string Path, string? PreviousPath) ComputePendingActionPath(SongDevice sd, TemplateNamingStrategy namingStrategy, HashSet<string> usedPaths)
    {
        if (sd.Song != null)
        {
            // A path typed by the user is used as is, once: it is cleared when the device applies it
            var basePath = sd.RequestedPath
                ?? namingStrategy.Generate(EntityConverter.ToSong(sd.Song), NamingMetadata.FromPath(sd.DevicePath));

            // usedPaths holds the SongDevice's own path too, which must not count as a collision: a path
            // that carries a collision counter would otherwise get the next counter instead of being kept
            var newPath = GetUniquePath(basePath, usedPaths, sd.DevicePath);

            return newPath != sd.DevicePath
                ? (newPath, sd.DevicePath)
                : (sd.DevicePath, null);
        }

        return (sd.DevicePath, null);
    }

    /// <inheritdoc />
    public string GetUniquePath(string basePath, HashSet<string> existingPaths) =>
        GetUniquePath(basePath, existingPaths, ownPath: null);

    private static string GetUniquePath(string basePath, HashSet<string> existingPaths, string? ownPath)
    {
        bool IsTaken(string path) => path != ownPath && existingPaths.Contains(path);

        if (!IsTaken(basePath))
        {
            return basePath;
        }

        var directory = Path.GetDirectoryName(basePath) ?? "";
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(basePath);
        var extension = Path.GetExtension(basePath);

        var counter = 2;
        string newPath;
        do
        {
            newPath = Path.Combine(directory, $"{fileNameWithoutExt} ({counter}){extension}");
            counter++;
        } while (IsTaken(newPath));

        return newPath;
    }
}
