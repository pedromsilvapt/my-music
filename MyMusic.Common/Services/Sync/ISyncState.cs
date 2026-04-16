namespace MyMusic.Common.Services.Sync;

using MyMusic.Common.Services.Sync.Types;

public interface ISyncState
{
    bool IsCancelled { get; }
    SyncOptions Options { get; }
}
