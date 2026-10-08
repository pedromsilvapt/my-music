using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Purchases;

/// <summary>
/// Queues the purchase of a song of a source, for the current user.
/// </summary>
public interface IPurchaseCreateService
{
    /// <summary>
    /// Creates a queued purchase of the song <paramref name="externalId"/> of the source. The purchases queue has to
    /// be scheduled afterwards for it to be picked up.
    /// </summary>
    /// <param name="sourceId">The ID of the source to purchase from.</param>
    /// <param name="externalId">The ID of the song in the source.</param>
    /// <param name="replaceSongId">
    /// When given, the purchased file is not imported as a new song: it replaces the audio of this song, which keeps
    /// its metadata (see <see cref="Songs.ISongFileReplaceService"/>).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The queued purchase, or <c>null</c> when <paramref name="replaceSongId"/> is not a song of the current user.
    /// </returns>
    Task<PurchasedSong?> CreateAsync(long sourceId, string externalId, long? replaceSongId = null,
        CancellationToken cancellationToken = default);
}
