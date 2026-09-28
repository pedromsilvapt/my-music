namespace MyMusic.Common.Services;

/// <summary>
///     The file changes of a database transaction. Each operation locks the paths it touches for the rest of that
///     transaction, and records how to undo itself. Originals are set aside as backups in
///     <c>.temp/tx-{id}</c> inside the repository (so on the same volume, where moving them is just a rename).
///     Committing keeps the changes and deletes the backups; anything else restores them.
/// </summary>
public interface IFileTransaction : IAsyncDisposable
{
    /// <summary>
    ///     Prepares <paramref name="path"/> to be written from scratch: an existing file there is moved aside. Undone
    ///     by deleting whatever was written and moving the original back.
    /// </summary>
    Task PrepareOverwriteAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Prepares the file at <paramref name="path"/> to be edited in place: a copy of it is kept as a backup. Undone
    ///     by moving the backup back over it.
    /// </summary>
    Task PrepareEditAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Moves a file to <paramref name="to"/>, creating its folder if needed. Never overwrites another file.</summary>
    Task MoveAsync(string from, string to, CancellationToken cancellationToken = default);

    /// <summary>Deletes a file, by moving it aside. Undone by moving it back.</summary>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}
