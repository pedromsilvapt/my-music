namespace MyMusic.CLI.Services.Sync;

using MyMusic.CLI.Services.Sync.Types;

public interface IUserPrompt
{
    /// <summary>
    /// Asks what to do with a real conflict. Only <paramref name="choices"/> can be answered.
    /// </summary>
    Task<ConflictResolution> PromptConflictResolutionAsync(string filePath, IReadOnlyList<ConflictResolution> choices, CancellationToken ct = default);
    Task<bool> ConfirmDeletionAsync(string filePath, CancellationToken ct = default);
}