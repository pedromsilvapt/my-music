using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Fixtures;

public class GenresFixtureTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    protected override bool NavigateOnInitialize => false;

    // Scenario: Seeding the sample genres creates every one of them
    //   Given a new user without any genres
    //   When the sample genres are seeded
    //   Then all 12 sample genres are created, each with an id and a name
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

    // Scenario: The seeded sample genres contain their well-known ones
    //   Given a new user without any genres
    //   When the sample genres are seeded
    //   Then the created genres include "Rock", "Pop" and "Metal"
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
