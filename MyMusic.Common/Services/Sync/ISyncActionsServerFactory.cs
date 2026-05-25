namespace MyMusic.Common.Services.Sync;

public interface ISyncActionsServerFactory
{
    ISyncActionsServer Create(MusicDbContext db, long sessionId, long deviceId, bool dryRun = false);
}