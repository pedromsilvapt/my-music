namespace MyMusic.Common.AudioIntegrity;

public record AudioIntegrityReport
{
    public required string FilePath { get; init; }
    public required AudioIntegrityStatus Status { get; init; }
    public required AudioFormat Format { get; init; }

    public long? Id3v2Size { get; init; }
    public long? AudioOffset { get; init; }

    public long? XingFrames { get; init; }

    public long ActualFrames { get; init; }
    public long FrameDelta => (XingFrames.HasValue ? ActualFrames - XingFrames.Value : 0);
    public double? TimeDeltaSeconds { get; init; }

    public bool MidFileId3v1 { get; init; }
    public bool MidFileId3v2 { get; init; }
    public bool LargeGap { get; init; }
    public bool BitrateCliff { get; init; }

    public int? FirstSectionBitrate { get; init; }
    public int? SecondSectionBitrate { get; init; }
    public long? SplitPoint { get; init; }

    public required ValidationStrategy Strategy { get; init; }
    public string? Details { get; init; }
}
