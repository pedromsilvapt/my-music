using Microsoft.Playwright;
using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Albums;

/// <summary>
/// Integration tests for the "New album" dialog of the albums page and of the artist detail page.
/// </summary>
public class AlbumCreateTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly ArtistsFixture _artists = new();
    private readonly AlbumsFixture _albums = new();

    [Fact]
    public async Task CreateAlbum_FromAlbumsPage_ShouldListAnEmptyAlbumOfThePickedArtist()
    {
        // Setup: a library with many artists, so the one to pick is far down the artist picker's list
        await _artists.SeedAsync(RequestContext, UserId);

        // Action: create an album from the toolbar of the albums page, searching for its artist in the picker
        var albumsPage = await new CreateAlbumFlow("Innuendo", "Queen", year: 1991).ExecuteAsync(Page);

        // Assert: the list should show the new album right away, without a reload
        await Assertions.Expect(albumsPage.Collection.GetCellsByExactText("name", "Innuendo")).ToHaveCountAsync(1);

        // Assert: the album should be empty, and belong to the picked artist
        await new ValidateAlbumCountsFlow("Innuendo", songsCount: 0).ExecuteAsync(Page);
        await new ValidateAlbumArtistFlow("Innuendo", "Queen").ExecuteAsync(Page);
    }

    // Scenario: An album created from an artist's page belongs to that artist without picking it
    //   Given a library with several artists
    //   When the user opens the new album dialog from an artist's page
    //   Then the dialog has that artist already picked
    //   When the user names the album and creates it
    //   Then the artist's page lists the new album right away
    //   And the album belongs to that artist
    [Fact]
    public async Task CreateAlbum_FromArtistDetailsPage_ShouldPreFillTheArtist()
    {
        // Setup: a library with several artists, so a dialog that picked the wrong one would be noticed
        await _artists.SeedAsync(RequestContext, UserId, ["Blur", "Queen", "Radiohead"]);

        // Action: create an album from the artist's detail page, where the dialog should open with the artist picked
        var artistPage = await new CreateArtistAlbumFlow("Queen", "Innuendo", year: 1991).ExecuteAsync(Page);

        // Assert: the artist's page should show the new album right away, without a reload
        await Assertions.Expect(artistPage.GetAlbums("Innuendo")).ToHaveCountAsync(1);
        await Assertions.Expect(artistPage.AlbumsCount).ToHaveAttributeAsync("data-count", "1");

        // Assert: the album should belong to the artist whose page it was created from
        await new ValidateAlbumArtistFlow("Innuendo", "Queen").ExecuteAsync(Page);
    }

    [Fact]
    public async Task CreateAlbum_WithANameTheArtistAlreadyHas_ShouldBeRejectedWithAnError()
    {
        // Setup: an artist that already has an album named like the one about to be created
        var queen = await _artists.SeedAsync(RequestContext, UserId, "Queen");
        await _albums.SeedAsync(RequestContext, UserId, [queen], [new SampleAlbum("Jazz", 1978)]);

        // Action: try to create a second album with that name for the same artist
        var error = await new CreateRejectedAlbumFlow("Jazz", "Queen").ExecuteAsync(Page);

        // Assert: the dialog should stay open explaining why
        error.ShouldBe("Artist 'Queen' already has an album named 'Jazz'");

        // Assert: after leaving the dialog behind, no second album should exist
        await Page.ReloadAsync();
        await new ValidateAlbumsNamedFlow("Jazz", count: 1).ExecuteAsync(Page);
    }

    [Fact]
    public async Task CreateAlbum_WithANameOnlyAnotherArtistHas_ShouldCreateASecondAlbum()
    {
        // Setup: two artists, one of which already has an album named like the one about to be created
        var queen = await _artists.SeedAsync(RequestContext, UserId, "Queen");
        await _artists.SeedAsync(RequestContext, UserId, "Blur");
        await _albums.SeedAsync(RequestContext, UserId, [queen], [new SampleAlbum("Greatest Hits", 1981)]);

        // Action: create an album with that name for the other artist — album names are only unique per artist
        await new CreateAlbumFlow("Greatest Hits", "Blur").ExecuteAsync(Page);

        // Assert: both albums should be listed
        await new ValidateAlbumsNamedFlow("Greatest Hits", count: 2).ExecuteAsync(Page);

        // Assert: the new album should count as one of its artist's albums
        await new ValidateArtistCountsFlow("Blur", songsCount: 0, albumsCount: 1).ExecuteAsync(Page);
    }
}
