namespace MyMusic.Common.Services;

/// <summary>
///     Starts <see cref="IFileTransaction"/>s: file changes in the music repository that are undone when the database
///     transaction they belong to is rolled back.
/// </summary>
public interface IFileTransactionService
{
    /// <summary>
    ///     Starts a file transaction bound to the current transaction of <paramref name="db"/>. Its changes are undone
    ///     if that transaction is rolled back, fails to commit, or is disposed without committing.
    /// </summary>
    /// <remarks>
    ///     Dispose the file transaction before (or together with) the database transaction, e.g. by declaring it with
    ///     <c>await using</c> right after it.
    /// </remarks>
    IFileTransaction Begin(MusicDbContext db);
}
