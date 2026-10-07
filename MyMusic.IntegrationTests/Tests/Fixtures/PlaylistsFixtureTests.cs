using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Fixtures;

public class PlaylistsFixtureTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    protected override bool NavigateOnInitialize => false;

    // Scenario: Seeding the sample playlists creates every one of them
    //   Given a new user without any playlists
    //   When the sample playlists are seeded
    //   Then all 3 sample playlists are created, each with an id and a name
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

    // Scenario: The seeded sample playlists have their well-known names
    //   Given a new user without any playlists
    //   When the sample playlists are seeded
    //   Then the created playlists include "Test Playlist 1", "Test Playlist 2" and "Test Playlist 3"
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
