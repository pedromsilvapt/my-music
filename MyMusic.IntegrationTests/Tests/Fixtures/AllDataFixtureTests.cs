using MyMusic.IntegrationTests.Base;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Fixtures;

public class AllDataFixtureTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    [Fact]
    public async Task SeedAsync_CreatesAllEntities()
    {
        var fixture = new IntegrationTests.Fixtures.Fixtures();
        await fixture.SeedAsync(RequestContext, UserId);

        fixture.TotalEntities.ShouldBeGreaterThan(0);

        fixture.DevicesData.Count.ShouldBe(3);
        fixture.PlaylistsData.Count.ShouldBe(3);
        fixture.SongsData.Count.ShouldBe(147);
        fixture.ArtistsData.Count.ShouldBe(102);
        fixture.AlbumsData.Count.ShouldBe(132);
        fixture.GenresData.Count.ShouldBe(12);
    }

    [Fact]
    public async Task AllFixtures_AreSeeded()
    {
        var fixture = new IntegrationTests.Fixtures.Fixtures();
        await fixture.SeedAsync(RequestContext, UserId);

        fixture.DevicesData.ShouldNotBeEmpty();
        fixture.PlaylistsData.ShouldNotBeEmpty();
        fixture.SongsData.ShouldNotBeEmpty();
        fixture.ArtistsData.ShouldNotBeEmpty();
        fixture.AlbumsData.ShouldNotBeEmpty();
        fixture.GenresData.ShouldNotBeEmpty();
    }
}
