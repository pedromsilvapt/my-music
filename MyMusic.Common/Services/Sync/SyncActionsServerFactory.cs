namespace MyMusic.Common.Services.Sync;

public class SyncActionsServerFactory : ISyncActionsServerFactory
{
    public ISyncActionsServer Create(MusicDbContext db, long sessionId, long deviceId, bool dryRun = false)
    {
        return new SyncActionsServer(db, sessionId);
    }
}