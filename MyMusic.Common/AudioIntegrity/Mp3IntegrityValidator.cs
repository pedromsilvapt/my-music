using System.IO.Abstractions;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyMusic.Common.AudioIntegrity;

public class Mp3IntegrityValidator(
    IOptions<AudioIntegrityConfig> config,
    IFFmpegRunner ffmpegRunner,
    IFileSystem fileSystem,
    ILogger<Mp3IntegrityValidator> logger) : IAudioIntegrityValidator
{
    public bool Supports(AudioFormat format) => format == AudioFormat.Mp3;

    public async Task<AudioIntegrityReport> ValidateAsync(string filePath, CancellationToken ct = default)
    {
        var strategy = config.Value.Strategy;

        // For FFmpeg-only strategy, pass the real file path directly without buffering into memory.
        if (strategy == ValidationStrategy.FFmpeg)
        {
            return await RunFfmpegAsync(filePath: filePath, buffer: null, heuristicReport: null, strategy, ct);
        }

        // Heuristic or Hybrid: read bytes for the span-based frame walker.
        var bytes = await fileSystem.File.ReadAllBytesAsync(filePath, ct);
        var heuristicReport = RunHeuristic(bytes.AsMemory(), filePath);

        if (strategy == ValidationStrategy.Heuristic)
        {
            return heuristicReport;
        }

        // Hybrid: run heuristic first, escalate to FFmpeg if Suspect or Corrupted.
        if (heuristicReport.Status is AudioIntegrityStatus.Suspect or AudioIntegrityStatus.Corrupted)
        {
            return await RunFfmpegAsync(filePath: filePath, buffer: null, heuristicReport: heuristicReport, strategy, ct);
        }

        return heuristicReport;
    }

    public async Task<AudioIntegrityReport> ValidateAsync(
        ReadOnlyMemory<byte> buffer,
        AudioFormat format,
        CancellationToken ct = default)
    {
        if (format != AudioFormat.Mp3)
            throw new NotSupportedException("This validator only supports MP3 files.");

        var strategy = config.Value.Strategy;
        var heuristicReport = RunHeuristic(buffer, "(buffer)");

        if (strategy == ValidationStrategy.Heuristic)
        {
            return heuristicReport;
        }

        if (strategy == ValidationStrategy.FFmpeg)
        {
            return await RunFfmpegAsync(filePath: null, buffer: buffer, heuristicReport: heuristicReport, strategy, ct);
        }

        // Hybrid: run heuristic first, escalate to FFmpeg if Suspect or Corrupted
        if (heuristicReport.Status is AudioIntegrityStatus.Suspect or AudioIntegrityStatus.Corrupted)
        {
            return await RunFfmpegAsync(filePath: null, buffer: buffer, heuristicReport: heuristicReport, strategy, ct);
        }

        return heuristicReport;
    }

    private async Task<AudioIntegrityReport> RunFfmpegAsync(
        string? filePath,
        ReadOnlyMemory<byte>? buffer,
        AudioIntegrityReport? heuristicReport,
        ValidationStrategy strategy,
        CancellationToken ct)
    {
        try
        {
            var stderrLines = await ffmpegRunner.RunAsync(
                filePath,
                buffer,
                config.Value.FFmpegTimeoutSeconds,
                ct);

            var hasErrors = stderrLines.Length > 0;

            if (hasErrors)
            {
                var baseReport = heuristicReport ?? CreateBaseReport(filePath ?? "(buffer)", strategy);
                return baseReport with
                {
                    Status = AudioIntegrityStatus.Corrupted,
                    Strategy = strategy,
                    Details = $"FFmpeg detected decode errors: {string.Join("; ", stderrLines.Take(5))}",
                };
            }

            if (heuristicReport == null)
            {
                // FFmpeg-only strategy with file path; no heuristic was run.
                return CreateBaseReport(filePath ?? "(buffer)", strategy) with
                {
                    Status = AudioIntegrityStatus.Clean,
                };
            }

            // FFmpeg says clean; heuristic said corrupted/suspect. Downgrade to Suspect.
            return heuristicReport with
            {
                Status = heuristicReport.Status == AudioIntegrityStatus.Corrupted
                    ? AudioIntegrityStatus.Suspect
                    : heuristicReport.Status,
                Strategy = strategy,
                Details = "Heuristic flagged issues but FFmpeg reported no decode errors.",
            };
        }
        catch (FFmpegNotFoundException)
        {
            // Explicit signal that ffmpeg is not available — rethrow so callers know.
            throw;
        }
        catch (Exception ex)
        {
            var pathForLog = filePath ?? "(buffer)";
            logger.LogWarning(ex, "FFmpeg validation failed for {FilePath}, falling back to heuristic.", pathForLog);

            if (heuristicReport != null)
            {
                return heuristicReport with
                {
                    Strategy = strategy,
                    Details = $"FFmpeg failed: {ex.Message}; falling back to heuristic.",
                };
            }

            // No heuristic fallback available (FFmpeg-only strategy with file path).
            throw new InvalidOperationException(
                $"FFmpeg validation failed and no heuristic fallback is available: {ex.Message}", ex);
        }
    }

    private static AudioIntegrityReport CreateBaseReport(string filePath, ValidationStrategy strategy)
    {
        return new AudioIntegrityReport
        {
            FilePath = filePath,
            Status = AudioIntegrityStatus.Clean,
            Format = AudioFormat.Mp3,
            Strategy = strategy,
        };
    }

    private AudioIntegrityReport RunHeuristic(ReadOnlyMemory<byte> buffer, string filePath)
    {
        var span = buffer.Span;
        var fileSize = span.Length;

        // Phase A: Locate audio data start
        int id3v2Size = 0;
        int audioOffset = 0;

        if (fileSize >= 10 &&
            span[0] == (byte)'I' &&
            span[1] == (byte)'D' &&
            span[2] == (byte)'3')
        {
            id3v2Size = ((span[6] & 0x7F) << 21)
                      | ((span[7] & 0x7F) << 14)
                      | ((span[8] & 0x7F) << 7)
                      |  (span[9] & 0x7F);
            audioOffset = 10 + id3v2Size;
        }

        // Phase B: Parse first MPEG frame & Xing/Info header
        long xingFrames = 0;
        long? xingFramesNullable = null;
        int firstBitrate = 0;
        int firstSampleRate = 0;
        int firstSamplesPerFrame = 0;
        int firstChannelMode = 0;

        if (audioOffset + 4 <= fileSize)
        {
            if (IsSyncWord(span, audioOffset))
            {
                var header = ParseMpegHeader(span, audioOffset);
                firstBitrate = header.BitrateKbps;
                firstSampleRate = header.SampleRate;
                firstSamplesPerFrame = header.SamplesPerFrame;
                firstChannelMode = header.ChannelMode;

                var frameSize = ComputeFrameSize(header);
                if (frameSize > 0 && audioOffset + frameSize <= fileSize)
                {
                    var xingOffset = FindXingOffset(span, audioOffset, frameSize, header);
                    if (xingOffset >= 0)
                    {
                        var flags = span[xingOffset + 7];
                        if ((flags & 0x01) != 0 && xingOffset + 11 <= fileSize)
                        {
                            xingFrames = ReadUInt32BigEndian(span, xingOffset + 8);
                            xingFramesNullable = xingFrames;
                        }
                    }
                }
            }
        }

        // Phase C: Walk all frames
        long actualFrames = 0;
        int lastValidFrameEnd = audioOffset;
        bool midFileId3v1 = false;
        bool midFileId3v2 = false;
        bool largeGap = false;
        bool bitrateCliff = false;
        int? firstSectionBitrate = null;
        int? secondSectionBitrate = null;
        int? splitPoint = null;

        int? currentSectionBitrate = null;
        int lastFrameEnd = audioOffset;
        long consecutiveFramesAtBitrate = 0;
        const long MinConsecutiveFramesForSection = 10;

        var offset = audioOffset;
        while (offset + 4 <= fileSize)
        {
            // Check for ID3v1
            if (offset + 3 <= fileSize &&
                span[offset] == (byte)'T' &&
                span[offset + 1] == (byte)'A' &&
                span[offset + 2] == (byte)'G')
            {
                if (offset != fileSize - 128)
                {
                    midFileId3v1 = true;
                }
                offset += 128;
                continue;
            }

            // Check for ID3v2 mid-file
            if (offset + 3 <= fileSize &&
                span[offset] == (byte)'I' &&
                span[offset + 1] == (byte)'D' &&
                span[offset + 2] == (byte)'3')
            {
                if (offset > audioOffset)
                {
                    midFileId3v2 = true;
                }
                if (offset + 10 <= fileSize)
                {
                    var tagSize = ((span[offset + 6] & 0x7F) << 21)
                                | ((span[offset + 7] & 0x7F) << 14)
                                | ((span[offset + 8] & 0x7F) << 7)
                                |  (span[offset + 9] & 0x7F);
                    offset += 10 + tagSize;
                    continue;
                }
                break;
            }

            if (!IsSyncWord(span, offset))
            {
                offset++;
                continue;
            }

            var h = ParseMpegHeader(span, offset);
            var fs = ComputeFrameSize(h);
            if (fs <= 0 || offset + fs > fileSize)
            {
                offset++;
                continue;
            }

            // Verify next frame has sync word (sanity check)
            if (offset + fs + 2 <= fileSize && !IsSyncWord(span, offset + fs))
            {
                if (offset + fs + 128 < fileSize)
                {
                    offset++;
                    continue;
                }
            }

            actualFrames++;
            lastValidFrameEnd = offset + fs;

            // Track bitrate sections for cliff detection
            var currentBitrate = h.BitrateKbps;
            if (currentBitrate > 0)
            {
                if (currentSectionBitrate == null)
                {
                    currentSectionBitrate = currentBitrate;
                    consecutiveFramesAtBitrate = 1;
                    firstSectionBitrate = currentBitrate;
                }
                else if (currentSectionBitrate == currentBitrate)
                {
                    consecutiveFramesAtBitrate++;
                }
                else
                {
                    var gap = offset - lastFrameEnd;
                    if (gap > config.Value.LargeGapThresholdBytes)
                    {
                        largeGap = true;
                    }

                    if (gap > 0 && consecutiveFramesAtBitrate >= MinConsecutiveFramesForSection)
                    {
                        var ratio = Math.Max(
                            (double)currentSectionBitrate.Value / currentBitrate,
                            (double)currentBitrate / currentSectionBitrate.Value);

                        if (ratio > config.Value.BitrateCliffRatio)
                        {
                            bitrateCliff = true;
                            if (firstSectionBitrate.HasValue && !secondSectionBitrate.HasValue)
                            {
                                secondSectionBitrate = currentBitrate;
                                splitPoint = offset;
                            }
                        }
                    }

                    currentSectionBitrate = currentBitrate;
                    consecutiveFramesAtBitrate = 1;
                }
            }

            lastFrameEnd = offset + fs;
            offset += fs;
        }

        // Phase D: Compute discrepancy
        long frameDelta = xingFramesNullable.HasValue ? actualFrames - xingFramesNullable.Value : 0;
        double? timeDeltaSeconds = null;
        if (xingFramesNullable.HasValue && firstSampleRate > 0 && firstSamplesPerFrame > 0)
        {
            timeDeltaSeconds = frameDelta * firstSamplesPerFrame / (double)firstSampleRate;
        }

        // Phase E: Decision matrix
        // Corrupted is reserved for files with significant structural corruption (|frameDelta| > 100).
        // Mid-file metadata, bitrate cliffs, and other anomalies are downgraded to Suspect.
        var status = AudioIntegrityStatus.Clean;
        string? details = null;

        if (xingFramesNullable.HasValue)
        {
            if (actualFrames + 1 < xingFramesNullable.Value)
            {
                status = AudioIntegrityStatus.Truncated;
                details = $"Truncated: Xing claims {xingFramesNullable.Value} frames but only {actualFrames} found.";
            }
            else if (Math.Abs(frameDelta) > config.Value.FrameTolerance)
            {
                status = AudioIntegrityStatus.Corrupted;
                details = $"Corrupted: frame delta = {frameDelta}, time delta = {timeDeltaSeconds:F1}s.";
                if (midFileId3v1 || midFileId3v2)
                    details += $" Mid-file metadata detected (ID3v1={midFileId3v1}, ID3v2={midFileId3v2}).";
                if (largeGap && bitrateCliff)
                    details += $" Bitrate cliff at byte {splitPoint} ({firstSectionBitrate} -> {secondSectionBitrate} kbps).";
            }
            else if (midFileId3v1 || midFileId3v2)
            {
                status = AudioIntegrityStatus.Suspect;
                details = $"Suspect: mid-file metadata detected (ID3v1={midFileId3v1}, ID3v2={midFileId3v2}), frame delta = {frameDelta}.";
            }
            else if (largeGap && bitrateCliff)
            {
                status = AudioIntegrityStatus.Suspect;
                details = $"Suspect: bitrate cliff detected at byte {splitPoint} ({firstSectionBitrate} -> {secondSectionBitrate} kbps).";
            }
            else if (Math.Abs(frameDelta) > 1)
            {
                status = AudioIntegrityStatus.Suspect;
                details = $"Suspect: small frame delta = {frameDelta}, time delta = {timeDeltaSeconds:F1}s.";
            }
        }
        else
        {
            if (midFileId3v1 || midFileId3v2 || (largeGap && bitrateCliff))
            {
                status = AudioIntegrityStatus.Suspect;
                details = "Suspect: mid-file metadata or bitrate cliff detected.";
            }
            else if (firstBitrate > 0 && firstSampleRate > 0 && firstSamplesPerFrame > 0)
            {
                int id3v1Size = 0;
                if (fileSize >= 128)
                {
                    var tagOffset = fileSize - 128;
                    if (span[tagOffset] == (byte)'T' && span[tagOffset + 1] == (byte)'A' && span[tagOffset + 2] == (byte)'G')
                    {
                        id3v1Size = 128;
                    }
                }

                var expectedBytes = (int)(actualFrames * ComputeFrameSize(
                    new MpegHeader(firstBitrate, firstSampleRate, firstSamplesPerFrame, firstChannelMode, 0, MpegLayer.LayerIII, MpegVersion.Mpeg1)));
                var actualAudioBytes = fileSize - id3v2Size - id3v1Size;
                var oneFrameSize = ComputeFrameSize(
                    new MpegHeader(firstBitrate, firstSampleRate, firstSamplesPerFrame, firstChannelMode, 0, MpegLayer.LayerIII, MpegVersion.Mpeg1));

                if (Math.Abs(expectedBytes - actualAudioBytes) > oneFrameSize)
                {
                    status = AudioIntegrityStatus.Suspect;
                    details = $"Suspect: file size discrepancy without Xing header (expected ~{expectedBytes} audio bytes, found {actualAudioBytes}).";
                }
            }
        }

        return new AudioIntegrityReport
        {
            FilePath = filePath,
            Status = status,
            Format = AudioFormat.Mp3,
            Id3v2Size = id3v2Size > 0 ? id3v2Size : null,
            AudioOffset = audioOffset > 0 ? audioOffset : null,
            XingFrames = xingFramesNullable,
            ActualFrames = actualFrames,
            TimeDeltaSeconds = timeDeltaSeconds,
            MidFileId3v1 = midFileId3v1,
            MidFileId3v2 = midFileId3v2,
            LargeGap = largeGap,
            BitrateCliff = bitrateCliff,
            FirstSectionBitrate = firstSectionBitrate,
            SecondSectionBitrate = secondSectionBitrate,
            SplitPoint = splitPoint,
            Strategy = ValidationStrategy.Heuristic,
            Details = details,
        };
    }

    private static bool IsSyncWord(ReadOnlySpan<byte> span, int offset)
    {
        return offset + 1 < span.Length && span[offset] == 0xFF && (span[offset + 1] & 0xE0) == 0xE0;
    }

    private readonly record struct MpegHeader(
        int BitrateKbps,
        int SampleRate,
        int SamplesPerFrame,
        int ChannelMode,
        int Padding,
        MpegLayer Layer,
        MpegVersion Version);

    private enum MpegVersion
    {
        Mpeg2_5 = 0,
        Reserved = 1,
        Mpeg2 = 2,
        Mpeg1 = 3,
    }

    private enum MpegLayer
    {
        Reserved = 0,
        LayerIII = 1,
        LayerII = 2,
        LayerI = 3,
    }

    private static MpegHeader ParseMpegHeader(ReadOnlySpan<byte> span, int offset)
    {
        var b1 = span[offset + 1];
        var b2 = span[offset + 2];
        var b3 = span[offset + 3];

        var version = (MpegVersion)((b1 >> 3) & 0x03);
        var layer = (MpegLayer)((b1 >> 1) & 0x03);
        var bitrateIndex = (b2 >> 4) & 0x0F;
        var sampleRateIndex = (b2 >> 2) & 0x03;
        var padding = (b2 >> 1) & 0x01;
        var channelMode = (b3 >> 6) & 0x03;

        int bitrateKbps = GetBitrateKbps(version, layer, bitrateIndex);
        int sampleRate = GetSampleRate(version, sampleRateIndex);
        int samplesPerFrame = GetSamplesPerFrame(version, layer);

        return new MpegHeader(bitrateKbps, sampleRate, samplesPerFrame, channelMode, padding, layer, version);
    }

    private static int ComputeFrameSize(MpegHeader h)
    {
        if (h.BitrateKbps <= 0 || h.SampleRate <= 0)
            return 0;

        var slotSize = h.Layer == MpegLayer.LayerI ? 4 : 1;
        var frameSize = (h.BitrateKbps * 1000 * h.SamplesPerFrame) / (h.SampleRate * 8) + (h.Padding * slotSize);
        return frameSize;
    }

    private static int FindXingOffset(ReadOnlySpan<byte> span, int frameOffset, int frameSize, MpegHeader header)
    {
        var fileSize = span.Length;

        int[] offsets;
        if (header.Version == MpegVersion.Mpeg1 && header.Layer == MpegLayer.LayerIII)
        {
            offsets = header.ChannelMode == 3 /* mono */ ? [21, 13] : [36, 21];
        }
        else if (header.Layer == MpegLayer.LayerIII)
        {
            offsets = header.ChannelMode == 3 /* mono */ ? [13, 9] : [21, 13];
        }
        else
        {
            offsets = header.ChannelMode == 3 /* mono */ ? [13, 9] : [21, 13];
        }

        foreach (var o in offsets)
        {
            var checkOffset = frameOffset + o;
            if (checkOffset + 4 <= fileSize)
            {
                if (span[checkOffset] == (byte)'X' &&
                    span[checkOffset + 1] == (byte)'i' &&
                    span[checkOffset + 2] == (byte)'n' &&
                    span[checkOffset + 3] == (byte)'g')
                {
                    // Xing
                    return checkOffset;
                }

                if (span[checkOffset] == (byte)'I' &&
                    span[checkOffset + 1] == (byte)'n' &&
                    span[checkOffset + 2] == (byte)'f' &&
                    span[checkOffset + 3] == (byte)'o')
                {
                    // Info
                    return checkOffset;
                }
            }
        }

        var endOffset = frameOffset + frameSize - 156;
        if (endOffset >= frameOffset && endOffset + 4 <= fileSize)
        {
            if (span[endOffset] == (byte)'X' &&
                span[endOffset + 1] == (byte)'i' &&
                span[endOffset + 2] == (byte)'n' &&
                span[endOffset + 3] == (byte)'g')
            {
                return endOffset;
            }

            if (span[endOffset] == (byte)'I' &&
                span[endOffset + 1] == (byte)'n' &&
                span[endOffset + 2] == (byte)'f' &&
                span[endOffset + 3] == (byte)'o')
            {
                return endOffset;
            }
        }

        return -1;
    }

    private static long ReadUInt32BigEndian(ReadOnlySpan<byte> span, int offset)
    {
        if (offset + 4 > span.Length)
            return 0;
        return ((long)span[offset] << 24)
             | ((long)span[offset + 1] << 16)
             | ((long)span[offset + 2] << 8)
             | span[offset + 3];
    }

    private static int GetBitrateKbps(MpegVersion version, MpegLayer layer, int index)
    {
        if (index == 0 || index == 0x0F)
            return 0;

        int[] table;
        if (version == MpegVersion.Mpeg1)
        {
            table = layer switch
            {
                MpegLayer.LayerI => new[] { 0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, 0 },
                MpegLayer.LayerII => new[] { 0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 0 },
                MpegLayer.LayerIII => new[] { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 },
                _ => new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            };
        }
        else
        {
            table = layer == MpegLayer.LayerI
                ? new[] { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, 0 }
                : new[] { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 };
        }

        return table[index];
    }

    private static int GetSampleRate(MpegVersion version, int index)
    {
        if (index == 3)
            return 0;

        var table = version switch
        {
            MpegVersion.Mpeg1 => new[] { 44100, 48000, 32000, 0 },
            MpegVersion.Mpeg2 => new[] { 22050, 24000, 16000, 0 },
            _ => new[] { 11025, 12000, 8000, 0 },
        };

        return table[index];
    }

    private static int GetSamplesPerFrame(MpegVersion version, MpegLayer layer)
    {
        if (layer == MpegLayer.LayerI)
            return 384;

        return version == MpegVersion.Mpeg1 ? 1152 : 576;
    }
}
