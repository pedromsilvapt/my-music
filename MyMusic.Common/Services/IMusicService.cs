using MyMusic.Common.Entities;
using MyMusic.Common.Models;

namespace MyMusic.Common.Services;

public interface IMusicService
{
    /// <summary>
    /// Creates a new music device, associated with the given repositoryId.
    /// </summary>
    /// <param name="db"></param>
    /// <param name="name"></param>
    /// <param name="ownerId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Device> CreateDevice(MusicDbContext db, string name, long ownerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a music to a device (if it is not added already)
    /// </summary>
    /// <param name="db"></param>
    /// <param name="deviceId"></param>
    /// <param name="song"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    Task AddSongsToDevice(MusicDbContext db, long deviceId, Song song,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a music from a device (if it is added already)
    /// </summary>
    /// <param name="db"></param>
    /// <param name="deviceId"></param>
    /// <param name="song"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    Task RemoveSongsToDevice(MusicDbContext db, long deviceId, Song song,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a dictionary with the list of songs matching the checksums provided in the list.
    /// </summary>
    /// <param name="db"></param>
    /// <param name="userId"></param>
    /// <param name="checksums"></param>
    /// <param name="checksumAlgorithm"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Dictionary<string, Song>> FindUserSongsByChecksum(MusicDbContext db, long userId,
        List<string> checksums, string checksumAlgorithm, CancellationToken cancellationToken = default);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="db"></param>
    /// <param name="job"></param>
    /// <param name="userId"></param>
    /// <param name="rootSourceFolder"></param>
    /// <param name="deviceIds"></param>
    /// <param name="searchOption"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="duplicatesStrategy"></param>
    /// <returns></returns>
    Task ImportRepositorySongs(MusicDbContext db, MusicImportJob job, long userId, string rootSourceFolder,
        IList<long>? deviceIds = null,
        DuplicateSongsHandlingStrategy duplicatesStrategy = DuplicateSongsHandlingStrategy.SkipIdentical,
        SearchOption searchOption = SearchOption.AllDirectories, CancellationToken cancellationToken = default);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="db"></param>
    /// <param name="job"></param>
    /// <param name="userId"></param>
    /// <param name="importSongsMetadataList"></param>
    /// <param name="deviceIds"></param>
    /// <param name="duplicatesStrategy"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task ImportRepositorySongs(MusicDbContext db, MusicImportJob job, long userId,
        IEnumerable<SongImportMetadata> importSongsMetadataList, IList<long>? deviceIds = null,
        DuplicateSongsHandlingStrategy duplicatesStrategy = DuplicateSongsHandlingStrategy.SkipIdentical,
        CancellationToken cancellationToken = default);
}