using Microsoft.Playwright;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;

namespace MyMusic.IntegrationTests.Fixtures;

public interface ISyncApplication : IAsyncDisposable
{
    long DeviceId { get; }
    string DeviceName { get; }

    Task InitializeAsync(IAPIRequestContext api, long userId, string userName, string? serverUrl = null);

    // Fixture helpers
    Task<string> CreateSongAsync(SampleSong song, string? relativePath = null, int? contentVariant = null);
    Task<List<string>> CreateSongsAsync(params (SampleSong Song, string Path)[] songs);
    Task<string> CreateUnreadableSongAsync(string relativePath);
    bool FileExists(string relativePath);
    string GetSongPath(string relativePath);
    /// <summary>Sets the naming template of the server device, where the device options live.</summary>
    Task SetNamingTemplateAsync(string namingTemplate);

    /// <summary>
    /// Sets a naming template in the application only, to preview in a dry run without saving it to the server
    /// device. Not supported by the clients yet.
    /// </summary>
    Task SetLocalNamingTemplateAsync(string namingTemplate);
    Task UpdateLocalFileMetadataAsync(string fileName, EditSongOptions options);

    /// <summary>Sets how many files the client sends to the server per check request, in every request.</summary>
    Task SetChunkSizeAsync(int chunkSize);

    /// <summary>Sets the rules that keep local paths out of the sync.</summary>
    Task SetExcludePatternsAsync(params string[] patterns);

    /// <summary>
    /// Sets the modified date the client gives to the files it downloads: <c>Now</c>, <c>ServerModifiedAt</c>
    /// or <c>ServerCreatedAt</c>.
    /// </summary>
    Task SetFileModifiedDateAsync(string source);

    /// <summary>The modified date of a local file, in UTC.</summary>
    DateTime GetFileModifiedAt(string relativePath);

    /// <summary>Bumps a local file's modification time without changing its content.</summary>
    void TouchLocalFile(string relativePath);

    /// <summary>Moves a local file to another path on the device, keeping its content.</summary>
    void MoveLocalFile(string fromRelativePath, string toRelativePath);
    List<string> GetAllFiles();
    void FileShouldExist(string relativePath, string? message = null);
    void FilesShouldExist(IEnumerable<string> relativePaths, string? message = null);
    void FileShouldNotExist(string relativePath, string? message = null);

    // The sync operation under test
    Task<SyncResult> SyncAsync(SyncOptions options);

    // Capability checks
    bool SupportsSyncDirection();
}
