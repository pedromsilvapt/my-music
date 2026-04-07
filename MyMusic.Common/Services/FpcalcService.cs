using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MyMusic.Common.Services;

public class FpcalcResult
{
    public uint[] Fingerprint { get; init; } = [];
    public double Duration { get; init; }
}

public interface IFpcalcService
{
    bool IsAvailable();
    Task<FpcalcResult?> FingerprintAsync(string filePath, double lengthSeconds = 15.0, int algorithm = 2, CancellationToken cancellationToken = default);
}

public class FpcalcService(ILogger<FpcalcService> logger) : IFpcalcService
{
    private readonly string? _cachedPath = FindFpcalcPath();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public bool IsAvailable()
    {
        var available = _cachedPath != null;
        logger.LogDebug("FpcalcService.IsAvailable() called, returning {Available}, path: {Path}", available, _cachedPath ?? "(null)");
        return available;
    }

    private static string? FindFpcalcPath()
    {
        string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "fpcalc.exe"
            : "fpcalc";

        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        
        foreach (var path in paths)
        {
            var fullPath = Path.Combine(path.Trim(), exeName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    public async Task<FpcalcResult?> FingerprintAsync(
        string filePath,
        double lengthSeconds = 15.0,
        int algorithm = 2,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("FingerprintAsync called for {FilePath}, fpcalc path: {FpcalcPath}", filePath, _cachedPath ?? "(null)");
        
        if (_cachedPath == null)
        {
            logger.LogWarning("fpcalc not available in PATH");
            return null;
        }

        if (!File.Exists(filePath))
        {
            logger.LogWarning("File not found for fingerprinting: {FilePath}", filePath);
            return null;
        }

        logger.LogDebug("File exists, running fpcalc for {FilePath}", filePath);

        var args = new List<string>
        {
            "-raw",
            "-json",
            "-length", lengthSeconds.ToString("F3"),
            "-algorithm", algorithm.ToString(),
            filePath
        };

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _cachedPath,
                Arguments = string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 3)
            {
                // Exit code 3 means fpcalc encountered an error but still generated a fingerprint
                // Try to parse the output anyway
                logger.LogDebug("fpcalc exited with code 3 (error but fingerprint generated) for {FilePath}", filePath);
            }
            else if (process.ExitCode != 0)
            {
                var errorMsg = stderr.Split('\n').FirstOrDefault()?.Trim() ?? "Unknown error";
                if (errorMsg.Contains("Empty fingerprint", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug("Empty fingerprint for {FilePath} (file too short)", filePath);
                    return null;
                }
                if (errorMsg.Contains("Error decoding audio frame", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug("Decode error for {FilePath}: {Error}", filePath, errorMsg);
                    return null;
                }
                logger.LogWarning("fpcalc failed for {FilePath}: {Error}", filePath, errorMsg);
                return null;
            }

            var jsonResult = JsonSerializer.Deserialize<FpcalcJsonResult>(stdout, JsonOptions);
            if (jsonResult == null || jsonResult.Fingerprint == null || jsonResult.Fingerprint.Length == 0)
            {
                logger.LogDebug("Empty or invalid fingerprint result for {FilePath}", filePath);
                return null;
            }

            return new FpcalcResult
            {
                Fingerprint = jsonResult.Fingerprint,
                Duration = jsonResult.Duration
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running fpcalc for {FilePath}", filePath);
            return null;
        }
    }

    private class FpcalcJsonResult
    {
        public uint[]? Fingerprint { get; set; }
        public double Duration { get; set; }
    }
}
