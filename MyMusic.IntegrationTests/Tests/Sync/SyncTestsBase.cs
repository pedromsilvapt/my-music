using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using Microsoft.Extensions.Configuration;
using MyMusic.OpenTelemetry.XUnit;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase(ITestOutputHelper output) : IntegrationTestBase(output)
{
    protected ISyncApplication App = null!;
    protected SongsFixture ServerSongs = null!;
    protected PlaylistsFixture Playlists = null!;

    protected abstract ISyncApplication CreateApplication(IConfiguration configuration, IntegrationTestTelemetry telemetry);

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        App = CreateApplication(Configuration, Telemetry);
        await App.InitializeAsync(RequestContext, UserId, UserName);
        ServerSongs = new SongsFixture();
        Playlists = new PlaylistsFixture();
    }

    public override async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
        await base.DisposeAsync();
    }
}
