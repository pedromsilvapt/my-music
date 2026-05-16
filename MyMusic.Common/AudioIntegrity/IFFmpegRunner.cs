namespace MyMusic.Common.AudioIntegrity;

public interface IFFmpegRunner
{
    Task<string[]> RunAsync(string? filePath, ReadOnlyMemory<byte>? buffer, int timeoutSeconds, CancellationToken ct);
}
