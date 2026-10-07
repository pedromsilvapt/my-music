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

    // Scenario: An artist created from the artists page is listed right away, without songs or albums
    //   Given a library without any artists
    //   When the user creates an artist from the artists page
    //   Then the new artist is listed, without reloading the page
    //   And the artist's detail page shows no songs and no albums
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

    // Scenario: Creating an artist with the name of an existing one creates a second artist with that name
    //   Given an artist already exists
    //   When the user creates another artist with the same name
    //   Then two artists with that name are listed
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
