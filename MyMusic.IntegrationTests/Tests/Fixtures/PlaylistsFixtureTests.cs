using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Fixtures;

public class PlaylistsFixtureTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    [Fact]
    public async Task SeedAsync_CreatesPlaylists()
    {
        var fixture = new PlaylistsFixture();
        var data = await fixture.SeedAsync(RequestContext, UserId);

        data.ShouldNotBeEmpty();
        data.Count.ShouldBe(3);

        foreach (var playlist in data)
        {
            playlist.Id.ShouldBeGreaterThan(0);
            playlist.Name.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task SeedAsync_ReturnsPlaylistsWithExpectedNames()
    {
        var fixture = new PlaylistsFixture();
        var data = await fixture.SeedAsync(RequestContext, UserId);

        var playlistNames = data.Select(p => p.Name).ToList();
        playlistNames.ShouldContain("Test Playlist 1");
        playlistNames.ShouldContain("Test Playlist 2");
        playlistNames.ShouldContain("Test Playlist 3");
    }
}
