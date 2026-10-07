using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Fixtures;

public class AlbumsFixtureTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    protected override bool NavigateOnInitialize => false;

    // Scenario: Seeding the sample albums creates every one of them
    //   Given a new user with the sample artists, and without any albums
    //   When the sample albums are seeded
    //   Then all 132 sample albums are created, each with an id and a name
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

    // Scenario: The seeded sample albums contain their well-known ones
    //   Given a new user with the sample artists, and without any albums
    //   When the sample albums are seeded
    //   Then the created albums include "Echoes, Silence, Patience & Grace", "The Fame Monster" and "Century Child"
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
