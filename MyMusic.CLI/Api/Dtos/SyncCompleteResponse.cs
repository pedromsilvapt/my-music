namespace MyMusic.CLI.Api.Dtos;

public record SyncCompleteResponse
{
    public required int CreateRemoteCount { get; init; }
    public required int UpdateRemoteCount { get; init; }
    public required int SkippedCount { get; init; }
    public required int CreateLocalCount { get; init; }
    public required int UpdateLocalCount { get; init; }
    public required int DeleteCount { get; init; }
    public required int LinkCount { get; init; }
    public required int UnlinkCount { get; init; }
    public required int RenameCount { get; init; }
    public required int ConflictCount { get; init; }
    public required int UpdateTimestampCount { get; init; }
    public required int ErrorCount { get; init; }
}