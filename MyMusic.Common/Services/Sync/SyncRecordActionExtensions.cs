using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

public static class SyncRecordActionExtensions
{
    /// <summary>
    /// Whether records of this action are performed by the client on the device, and therefore
    /// must be acknowledged by the client before the session can be committed.
    /// </summary>
    public static bool IsClientAction(this SyncRecordAction action) =>
        action is SyncRecordAction.CreateLocal or SyncRecordAction.UpdateLocal
            or SyncRecordAction.Unlink or SyncRecordAction.Rename or SyncRecordAction.DeleteLocal;
}
