using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyMusic.Common.AudioIntegrity;

public class AudioIntegrityService(
    IOptions<AudioIntegrityConfig> config,
    IEnumerable<IAudioIntegrityValidator> validators,
    ILogger<AudioIntegrityService> logger) : IAudioIntegrityService
{
    public async Task<AudioIntegrityReport> ValidateAsync(string filePath, CancellationToken ct = default)
    {
        var format = InferFormat(filePath);
        var validator = validators.FirstOrDefault(v => v.Supports(format));

        if (validator == null)
        {
            throw new NotSupportedException($"No validator found for audio format '{format}' (file: {filePath}).");
        }

        logger.LogDebug("Validating {FilePath} with format {Format}", filePath, format);
        return await validator.ValidateAsync(filePath, ct);
    }

    public async Task<AudioIntegrityReport> ValidateAsync(
        ReadOnlyMemory<byte> buffer,
        AudioFormat format,
        CancellationToken ct = default)
    {
        var validator = validators.FirstOrDefault(v => v.Supports(format));

        if (validator == null)
        {
            throw new NotSupportedException($"No validator found for audio format '{format}'.");
        }

        logger.LogDebug("Validating buffer with format {Format}", format);
        return await validator.ValidateAsync(buffer, format, ct);
    }

    private static AudioFormat InferFormat(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".mp3" => AudioFormat.Mp3,
            _ => throw new NotSupportedException($"Cannot infer audio format from extension '{ext}' for file '{filePath}'."),
        };
    }
}
