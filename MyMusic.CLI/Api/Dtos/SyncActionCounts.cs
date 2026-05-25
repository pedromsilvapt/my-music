namespace MyMusic.CLI.Api.Dtos;

public record SyncActionCounts
{
    public int CreateRemoteCount { get; init; }
    public int UpdateRemoteCount { get; init; }
    public int SkippedCount { get; init; }
    public int CreateLocalCount { get; init; }
    public int UpdateLocalCount { get; init; }
    public int DeleteCount { get; init; }
    public int LinkCount { get; init; }
    public int UnlinkCount { get; init; }
    public int RenameCount { get; init; }
    public int ConflictCount { get; init; }
    public int UpdateTimestampCount { get; init; }
    public int ErrorCount { get; init; }
}