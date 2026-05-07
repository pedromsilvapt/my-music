using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Fixtures;

public class ArtistsFixtureTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
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
