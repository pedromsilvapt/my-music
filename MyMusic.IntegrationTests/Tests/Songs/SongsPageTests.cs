using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Songs;

public class SongsPageTests : IntegrationTestBase
{
    [Fact]
    public async Task SongsPage_ShouldDisplayCollection()
    {
        // Seed songs data for the current user
        var songs = new SongsFixture();
        await songs.SeedAsync(RequestContext, UserId);

        // Navigate to the songs page via the navbar
        var home = new HomePage(Page);
        var songsPage = await home.Navbar.GoToSongsAsync();

        // Verify the songs collection displays at least one row
        var rowCount = await songsPage.Collection.GetRowCountAsync();
        rowCount.ShouldBeGreaterThan(0, "Songs collection should have at least one row");
    }
}
