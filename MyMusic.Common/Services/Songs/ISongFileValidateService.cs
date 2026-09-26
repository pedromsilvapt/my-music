namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Checks whether a song file can be imported into the library, without importing it.
/// </summary>
public interface ISongFileValidateService
{
    /// <summary>
    /// Reads the file's metadata the same way the importer does.
    /// </summary>
    /// <param name="filePath">The path of the file to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>null</c> when the file is importable; otherwise, a message describing why it is not.</returns>
    Task<string?> ValidateAsync(string filePath, CancellationToken cancellationToken = default);
}
