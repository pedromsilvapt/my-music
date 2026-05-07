using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Fixtures;

public class AlbumsFixtureTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    [Fact]
    public async Task SeedAsync_CreatesAlbums()
    {
        var artistsFixture = new ArtistsFixture();
        var artists = await artistsFixture.SeedAsync(RequestContext, UserId);

        var fixture = new AlbumsFixture();
        var data = await fixture.SeedAsync(RequestContext, UserId, artists);

        data.ShouldNotBeEmpty();
        data.Count.ShouldBe(132);

        foreach (var album in data)
        {
            album.Id.ShouldBeGreaterThan(0);
            album.Name.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task SeedAsync_ReturnsAlbumsWithExpectedNames()
    {
        var artistsFixture = new ArtistsFixture();
        var artists = await artistsFixture.SeedAsync(RequestContext, UserId);

        var fixture = new AlbumsFixture();
        var data = await fixture.SeedAsync(RequestContext, UserId, artists);

        var albumNames = data.Select(a => a.Name).ToList();
        albumNames.ShouldContain("Echoes, Silence, Patience & Grace");
        albumNames.ShouldContain("The Fame Monster");
        albumNames.ShouldContain("Century Child");
    }
}
