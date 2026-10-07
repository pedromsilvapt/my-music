using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Fixtures;

public class ArtistsFixtureTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    protected override bool NavigateOnInitialize => false;

    // Scenario: Seeding the sample artists creates every one of them
    //   Given a new user without any artists
    //   When the sample artists are seeded
    //   Then all 102 sample artists are created, each with an id and a name
    [Fact]
    public async Task SeedAsync_CreatesArtists()
    {
        var fixture = new ArtistsFixture();
        var data = await fixture.SeedAsync(RequestContext, UserId);

        data.ShouldNotBeEmpty();
        data.Count.ShouldBe(102);

        foreach (var artist in data)
        {
            artist.Id.ShouldBeGreaterThan(0);
            artist.Name.ShouldNotBeNullOrEmpty();
        }
    }

    // Scenario: The seeded sample artists contain their well-known ones
    //   Given a new user without any artists
    //   When the sample artists are seeded
    //   Then the created artists include "Foo Fighters", "Lady Gaga" and "Nightwish"
    [Fact]
    public async Task SeedAsync_ReturnsArtistsWithExpectedNames()
    {
        var fixture = new ArtistsFixture();
        var data = await fixture.SeedAsync(RequestContext, UserId);

        var artistNames = data.Select(a => a.Name).ToList();
        artistNames.ShouldContain("Foo Fighters");
        artistNames.ShouldContain("Lady Gaga");
        artistNames.ShouldContain("Nightwish");
    }
}
