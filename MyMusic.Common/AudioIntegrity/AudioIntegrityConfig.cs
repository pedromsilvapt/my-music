namespace MyMusic.Common.AudioIntegrity;

public class AudioIntegrityConfig
{
    /// <summary>
    /// Which validation engine to use.
    /// </summary>
    public ValidationStrategy Strategy { get; set; } = ValidationStrategy.Heuristic;

    /// <summary>
    /// Absolute path to the ffmpeg executable. If null, assumed to be on PATH.
    /// </summary>
    public string? FFmpegPath { get; set; }

    /// <summary>
    /// Timeout in seconds when spawning ffmpeg. Default 30.
    /// </summary>
    public int FFmpegTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Number of frames discrepancy before a file is flagged Corrupted.
    /// </summary>
    public int FrameTolerance { get; set; } = 100;

    /// <summary>
    /// Minimum non-audio gap (in bytes) to consider a "large gap".
    /// </summary>
    public int LargeGapThresholdBytes { get; set; } = 2048;

    /// <summary>
    /// Ratio threshold for a bitrate cliff (e.g. 2.0 means 2x change).
    /// </summary>
    public double BitrateCliffRatio { get; set; } = 2.0;
}
