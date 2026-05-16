namespace MyMusic.Common.AudioIntegrity;

public interface IAudioIntegrityValidator
{
    /// <summary>
    /// Returns true if this validator handles the given format.
    /// </summary>
    bool Supports(AudioFormat format);

    /// <summary>
    /// Validates a file on disk.
    /// </summary>
    Task<AudioIntegrityReport> ValidateAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Validates an in-memory buffer.
    /// </summary>
    Task<AudioIntegrityReport> ValidateAsync(
        ReadOnlyMemory<byte> buffer,
        AudioFormat format,
        CancellationToken ct = default);
}
