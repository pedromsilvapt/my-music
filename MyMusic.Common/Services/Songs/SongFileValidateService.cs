using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Targets;

namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Default implementation of <see cref="ISongFileValidateService"/>.
/// </summary>
public class SongFileValidateService(
    IFileSystem fileSystem,
    ILogger<SongFileValidateService> logger) : ISongFileValidateService
{
    /// <inheritdoc />
    public async Task<string?> ValidateAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            await new FileTarget(fileSystem) { FilePath = filePath }.ReadMetadata(cancellationToken);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Song file {FilePath} is not importable", filePath);
            return $"Cannot read song metadata: {ex.Message}";
        }
    }
}
