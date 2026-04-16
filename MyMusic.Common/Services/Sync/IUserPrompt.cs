namespace MyMusic.Common.Services.Sync;

using MyMusic.Common.Services.Sync.Types;

public interface IUserPrompt
{
    Task<ConflictResolution> PromptConflictResolutionAsync(string filePath, CancellationToken ct = default);
    Task<bool> ConfirmDeletionAsync(string filePath, CancellationToken ct = default);
}
