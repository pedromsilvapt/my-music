using Microsoft.Playwright;
using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;

namespace MyMusic.IntegrationTests.Tests.Artists;

/// <summary>
/// Integration tests for the "New artist" dialog of the artists page.
/// </summary>
public class ArtistCreateTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly ArtistsFixture _artists = new();

    [Fact]
    public async Task CreateArtist_FromArtistsPage_ShouldListAnArtistWithoutSongsOrAlbums()
    {
        // Action: create an artist from the toolbar of the (empty) artists page
        var artistsPage = await new CreateArtistFlow("Portishead").ExecuteAsync(Page);

        // Assert: the list should show the new artist right away, without a reload
        await Assertions.Expect(artistsPage.Collection.GetCellsByExactText("name", "Portishead")).ToHaveCountAsync(1);

        // Assert: the artist should have its own detail page, with nothing in it yet
        await new ValidateArtistCountsFlow("Portishead", songsCount: 0, albumsCount: 0).ExecuteAsync(Page);
    }

    [Fact]
    public async Task CreateArtist_WithTheNameOfAnExistingArtist_ShouldCreateASecondArtist()
    {
        // Setup: an artist already named like the one about to be created
        await _artists.SeedAsync(RequestContext, UserId, "Blur");

        // Action: create another artist with that same name — artist names are not unique, so it should be accepted
        await new CreateArtistFlow("Blur").ExecuteAsync(Page);

        // Assert: both artists should be listed
        await new ValidateArtistsNamedFlow("Blur", count: 2).ExecuteAsync(Page);
    }
}
