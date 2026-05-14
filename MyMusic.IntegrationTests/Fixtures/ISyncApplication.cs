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
    Task<string> CreateSongAsync(SampleSong song, string? relativePath = null);
    Task<List<string>> CreateSongsAsync(params (SampleSong Song, string Path)[] songs);
    bool FileExists(string relativePath);
    string GetSongPath(string relativePath);
    Task SetNamingTemplateAsync(string namingTemplate);
    Task UpdateLocalFileMetadataAsync(string fileName, EditSongOptions options);
    List<string> GetAllFiles();
    void FileShouldExist(string relativePath, string? message = null);
    void FilesShouldExist(IEnumerable<string> relativePaths, string? message = null);
    void FileShouldNotExist(string relativePath, string? message = null);

    // The sync operation under test
    Task<SyncResult> SyncAsync(SyncOptions options);

    // Capability checks
    bool SupportsSyncDirection();
}
