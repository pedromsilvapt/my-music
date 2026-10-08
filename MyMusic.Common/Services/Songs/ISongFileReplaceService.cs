using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Replaces the audio of a song with the one of another file, keeping everything known about the song.
/// </summary>
public interface ISongFileReplaceService
{
    /// <summary>
    /// Puts the file at <paramref name="sourceFilePath"/> in the place of the song's file. The tags the new file came
    /// with are discarded: it gets the ones of the song's current file, and then the song's metadata, so only the
    /// audio changes. The song's devices are marked to download it again.
    /// When the new file has another format, the song's file and its copies on the devices change extension.
    /// </summary>
    /// <param name="songId">The ID of the song, which must belong to the current user.</param>
    /// <param name="sourceFilePath">
    /// The new file, outside the music repository. Its tags are rewritten in place, and it is left where it is.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated song.</returns>
    /// <exception cref="ValidationException">The file is not a song file that can be imported.</exception>
    Task<Song> ReplaceAsync(long songId, string sourceFilePath, CancellationToken cancellationToken = default);
}
