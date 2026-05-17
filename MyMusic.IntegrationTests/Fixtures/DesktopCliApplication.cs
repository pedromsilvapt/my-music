using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using MyMusic.OpenTelemetry.XUnit;

namespace MyMusic.IntegrationTests.Fixtures;

public class DesktopCliApplication : ISyncApplication
{
    private readonly CliTestFixture _fixture;
    private readonly CliRunner _runner;

    public long DeviceId => _fixture.DeviceId;
    public string DeviceName => _fixture.DeviceName;

    public DesktopCliApplication(IConfiguration configuration, IntegrationTestTelemetry telemetry)
    {
        _fixture = new CliTestFixture();
        _runner = new CliRunner(configuration, telemetry);
    }

    public DesktopCliApplication(IConfiguration configuration, IntegrationTestTelemetry telemetry, string? deviceName = null, string? namingTemplate = null)
    {
        _fixture = new CliTestFixture(deviceName, namingTemplate);
        _runner = new CliRunner(configuration, telemetry);
    }

    public Task InitializeAsync(IAPIRequestContext api, long userId, string userName, string? serverUrl = null)
        => _fixture.InitializeAsync(api, userId, userName, serverUrl);

    public Task<string> CreateSongAsync(SampleSong song, string? relativePath = null, int? contentVariant = null)
        => _fixture.CreateSongAsync(song, relativePath, contentVariant);

    public Task<List<string>> CreateSongsAsync(params (SampleSong Song, string Path)[] songs)
        => _fixture.CreateSongsAsync(songs);

    public bool FileExists(string relativePath)
        => _fixture.FileExists(relativePath);

    public string GetSongPath(string relativePath)
        => _fixture.GetSongPath(relativePath);

    public Task SetNamingTemplateAsync(string namingTemplate)
        => _fixture.SetNamingTemplateAsync(namingTemplate);

    public Task UpdateLocalFileMetadataAsync(string fileName, EditSongOptions options)
        => _fixture.UpdateLocalFileMetadataAsync(fileName, options);

    public List<string> GetAllFiles()
        => _fixture.GetAllFiles();

    public void FileShouldExist(string relativePath, string? message = null)
        => _fixture.FileShouldExist(relativePath, message);

    public void FilesShouldExist(IEnumerable<string> relativePaths, string? message = null)
        => _fixture.FilesShouldExist(relativePaths, message);

    public void FileShouldNotExist(string relativePath, string? message = null)
        => _fixture.FileShouldNotExist(relativePath, message);

    public async Task<SyncResult> SyncAsync(SyncOptions options)
    {
        var cliResult = await _runner.SyncAsync(
            _fixture,
            force: options.Force,
            autoConfirm: options.AutoConfirm,
            dryRun: options.DryRun,
            direction: options.Direction);

        return SyncResult.ParseCliOutput(cliResult.ExitCode, cliResult.StandardOutput);
    }

    public bool SupportsSyncDirection() => true;

    public async ValueTask DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
