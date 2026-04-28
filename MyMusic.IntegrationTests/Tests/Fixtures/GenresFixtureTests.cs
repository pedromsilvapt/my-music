using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Fixtures;

public class GenresFixtureTests : IntegrationTestBase
{
    [Fact]
    public async Task SeedAsync_CreatesGenres()
    {
        var fixture = new GenresFixture();
        var data = await fixture.SeedAsync(RequestContext, UserId);

        data.ShouldNotBeEmpty();
        data.Count.ShouldBe(12);

        foreach (var genre in data)
        {
            genre.Id.ShouldBeGreaterThan(0);
            genre.Name.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task SeedAsync_ReturnsGenresWithExpectedNames()
    {
        var fixture = new GenresFixture();
        var data = await fixture.SeedAsync(RequestContext, UserId);

        var genreNames = data.Select(g => g.Name).ToList();
        genreNames.ShouldContain("Rock");
        genreNames.ShouldContain("Pop");
        genreNames.ShouldContain("Metal");
    }
}
