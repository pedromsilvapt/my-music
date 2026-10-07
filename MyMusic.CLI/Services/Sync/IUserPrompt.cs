namespace MyMusic.CLI.Services.Sync;

using MyMusic.CLI.Services.Sync.Types;

public interface IUserPrompt
{
    /// <summary>
    /// Asks what to do with a real conflict. Only <paramref name="choices"/> can be answered. The answer can
    /// be given for every remaining conflict of the sync.
    /// </summary>
    Task<PromptAnswer<ConflictResolution>> PromptConflictResolutionAsync(string filePath, IReadOnlyList<ConflictResolution> choices, CancellationToken ct = default);

    /// <summary>
    /// Asks whether to delete a local file. The answer can be given for every remaining deletion of the sync.
    /// </summary>
    Task<PromptAnswer<bool>> ConfirmDeletionAsync(string filePath, CancellationToken ct = default);
}
