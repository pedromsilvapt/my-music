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
            var metadata = EntityConverter.ToSong(sd.Song);
            var naming = NamingMetadata.FromPath(sd.DevicePath);
            var basePath = namingStrategy.Generate(metadata, naming);
            var newPath = basePath == sd.DevicePath
                ? basePath
                : GetUniquePath(basePath, usedPaths);

            return newPath != sd.DevicePath
                ? (newPath, sd.DevicePath)
                : (sd.DevicePath, null);
        }

        return (sd.DevicePath, null);
    }

    /// <inheritdoc />
    public string GetUniquePath(string basePath, HashSet<string> existingPaths)
    {
        if (!existingPaths.Contains(basePath))
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
        } while (existingPaths.Contains(newPath));

        return newPath;
    }
}