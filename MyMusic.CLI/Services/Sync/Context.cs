namespace MyMusic.CLI.Services.Sync;

using MyMusic.Common.Services.Sync;
using MyMusic.Common.Services.Sync.Types;

public record SyncContext
{
    public long DeviceId { get; init; }
    public required string RepositoryPath { get; init; }
    public long SessionId { get; set; }
    public SyncOptions Options { get; init; } = new();
    public SyncResult Result { get; set; } = new();
    public HashSet<string> UploadedPaths { get; } = [];
    public HashSet<string> PendingDownloadPaths { get; set; } = [];
    public List<PendingActionItem> PendingActions { get; set; } = [];
}
