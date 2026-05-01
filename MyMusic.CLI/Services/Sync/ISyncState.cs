namespace MyMusic.CLI.Services.Sync;

using MyMusic.CLI.Services.Sync.Types;

public interface ISyncState
{
    bool IsCancelled { get; }
    SyncOptions Options { get; }
}