using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyMusic.Common.AudioIntegrity;

public class FFmpegRunner(
    IOptions<AudioIntegrityConfig> config,
    ILogger<FFmpegRunner> logger) : IFFmpegRunner
{
    public async Task<string[]> RunAsync(string? filePath, ReadOnlyMemory<byte>? buffer, int timeoutSeconds, CancellationToken ct)
    {
        var ffmpegPath = GetFfmpegPath();
        var args = new List<string>
        {
            "-hide_banner",
            "-loglevel", "error",
            "-i", filePath ?? "pipe:0",
            "-f", "null",
            "-",
        };

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = buffer.HasValue,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process();
        process.StartInfo = psi;

        var stderrBuilder = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                stderrBuilder.AppendLine(e.Data);
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new FFmpegNotFoundException($"Failed to start ffmpeg. Ensure ffmpeg is installed and available on PATH. Error: {ex.Message}", ex);
        }

        process.BeginErrorReadLine();

        if (buffer.HasValue)
        {
            try
            {
                await process.StandardInput.BaseStream.WriteAsync(buffer.Value, ct);
                await process.StandardInput.BaseStream.FlushAsync(ct);
                process.StandardInput.Close();
            }
            catch (Exception ex)
            {
                try { process.Kill(); } catch { /* ignore */ }
                throw new InvalidOperationException($"Failed to write buffer to ffmpeg stdin: {ex.Message}", ex);
            }
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!ct.IsCancellationRequested)
            {
                try { process.Kill(); } catch { /* ignore */ }
                throw new InvalidOperationException($"ffmpeg timed out after {timeoutSeconds} seconds.");
            }
            throw;
        }

        var stderr = stderrBuilder.ToString();
        var lines = stderr.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        return lines;
    }

    private string GetFfmpegPath()
    {
        return config.Value.FFmpegPath
            ?? Environment.GetEnvironmentVariable("FFMPEG_PATH")
            ?? "ffmpeg";
    }
}
