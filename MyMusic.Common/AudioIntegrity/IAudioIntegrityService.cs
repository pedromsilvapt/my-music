namespace MyMusic.Common.AudioIntegrity;

public interface IAudioIntegrityService
{
    Task<AudioIntegrityReport> ValidateAsync(string filePath, CancellationToken ct = default);
    Task<AudioIntegrityReport> ValidateAsync(
        ReadOnlyMemory<byte> buffer,
        AudioFormat format,
        CancellationToken ct = default);
}
