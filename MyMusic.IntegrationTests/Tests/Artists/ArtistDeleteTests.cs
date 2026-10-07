using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Artists;

/// <summary>
/// Integration tests for deleting an artist, from the artists list and from the artist's detail page.
/// </summary>
public class ArtistDeleteTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly ArtistsFixture _artists = new();
    private readonly AlbumsFixture _albums = new();
    private readonly SongsFixture _songs = new();

    // Scenario: An artist without songs is deleted along with its albums, without a warning
    //   Given two artists, each with an empty album
    //   When the user deletes one of the artists from the artists list
    //   Then the confirmation does not warn about any song
    //   And the artist and its album are gone
    //   And the other artist keeps its album
    [Fact]
    public async Task DeleteArtist_WithoutSongs_ShouldDeleteItAndItsAlbumsWithoutAWarning()
    {
        // Setup: two artists, each with an empty album
        var queen = await _artists.SeedAsync(RequestContext, UserId, "Queen");
        var blur = await _artists.SeedAsync(RequestContext, UserId, "Blur");
        await _albums.SeedAsync(RequestContext, UserId, [queen], [new SampleAlbum("Jazz", 1978)]);
        await _albums.SeedAsync(RequestContext, UserId, [blur], [new SampleAlbum("Parklife", 1994)]);

        // Action: delete one of the artists from its row in the artists list
        var warning = await new DeleteArtistFlow("Queen").ExecuteAsync(Page);

        // Assert: no song references the artist, so the confirmation should not warn about any
        warning.ShouldBeNull();

        // Assert: the artist and its album should be gone, and the other artist should be untouched
        await new ShouldArtistExistFlow("Queen", shouldExist: false).ExecuteAsync(Page);
        await new ShouldAlbumExistFlow("Jazz", shouldExist: false).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Blur", songsCount: 0, albumsCount: 1).ExecuteAsync(Page);
    }

    // Scenario: Deleting an artist featured in another artist's song takes it out of the song
    //   Given a song by two artists, in an album of the first one
    //   When the user deletes the featured artist from its detail page
    //   Then the confirmation warns that one song still references the artist
    //   And the artist is gone
    //   And the song keeps its album, with only its other artist
    [Fact]
    public async Task DeleteArtist_FeaturedInAnotherArtistsSong_ShouldWarnAndTakeItOutOfTheSong()
    {
        // Setup: a song by two artists, in an album of the first one
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[6]); // Faithless, Bebe Rexha - New Religion

        // Action: delete the featured artist from its detail page
        var warning = await new DeleteArtistFlow("Bebe Rexha", fromDetailsPage: true).ExecuteAsync(Page);

        // Assert: the confirmation should warn about the song that still references the artist
        warning.ShouldNotBeNull().ShouldStartWith("1 song still references this artist.");

        // Assert: the artist should be gone, and the song should keep its album with only its other artist
        await new ShouldArtistExistFlow("Bebe Rexha", shouldExist: false).ExecuteAsync(Page);
        await new ValidateSongDetailsFlow("New Religion",
            new(Artists: ["Faithless"], Album: "New Religion", AlbumArtist: "Faithless")).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Faithless", songsCount: 1, albumsCount: 1).ExecuteAsync(Page);
    }

    // Scenario: Deleting the only artist of some songs leaves them without an artist and an album
    //   Given an artist with two songs in one album, and no other artist in them
    //   When the user deletes the artist from the artists list
    //   Then the confirmation warns that two songs still reference the artist
    //   And the artist and its album are gone
    //   And both songs belong to "(No Artist)", in its "(No Album)"
    [Fact]
    public async Task DeleteArtist_SoleArtistOfItsSongs_ShouldWarnAndMoveThemToNoArtistAndNoAlbum()
    {
        // Setup: an artist with two songs in one album, and no other artist to leave them to
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[46]); // Amaranthe - Burn With Me from The Nexus
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[47]); // Amaranthe - The Nexus from The Nexus

        // Action: delete the artist from its row in the artists list
        var warning = await new DeleteArtistFlow("Amaranthe").ExecuteAsync(Page);

        // Assert: the confirmation should warn about both songs
        warning.ShouldNotBeNull().ShouldStartWith("2 songs still reference this artist.");

        // Assert: the artist and its album should be gone
        await new ShouldArtistExistFlow("Amaranthe", shouldExist: false).ExecuteAsync(Page);
        await new ShouldAlbumExistFlow("The Nexus", shouldExist: false).ExecuteAsync(Page);

        // Assert: both songs should now belong to "(No Artist)", in its "(No Album)"
        await new ValidateArtistCountsFlow("(No Artist)", songsCount: 2, albumsCount: 1).ExecuteAsync(Page);
        await new ValidateSongDetailsFlow("Burn With Me",
            new(Artists: ["(No Artist)"], Album: "(No Album)", AlbumArtist: "(No Artist)")).ExecuteAsync(Page);
    }

    // Scenario: Artists selected together are deleted at once, with a single warning
    //   Given a song by two artists, a song by a third artist, and an artist without songs
    //   When the user selects both artists of the first song and the one without songs, and deletes them
    //   Then a single confirmation warns about the shared song, counting it only once
    //   And the selected artists and their album are gone
    //   And the artist left out keeps its song and album
    //   And the song left without artists belongs to "(No Artist)", in its "(No Album)"
    [Fact]
    public async Task DeleteArtists_SelectedTogether_ShouldWarnOnceAndDeleteThemAll()
    {
        // Setup: a song by two artists, a song by a third one, and an artist without songs
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[6]); // Faithless, Bebe Rexha - New Religion
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[46]); // Amaranthe - Burn With Me from The Nexus
        await _artists.SeedAsync(RequestContext, UserId, "Queen");

        // Action: select both artists of the first song and the empty one, and delete them through the selection's actions
        var warning = await new DeleteArtistsBulkFlow("Faithless", "Bebe Rexha", "Queen").ExecuteAsync(Page);

        // Assert: a single confirmation should count the song the two artists share only once
        warning.ShouldNotBeNull().ShouldStartWith("1 song still references these artists.");

        // Assert: the selected artists and their album should be gone, and the artist left out should be untouched
        await new ShouldArtistExistFlow("Faithless", shouldExist: false).ExecuteAsync(Page);
        await new ShouldArtistExistFlow("Queen", shouldExist: false).ExecuteAsync(Page);
        await new ShouldAlbumExistFlow("New Religion", shouldExist: false).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Amaranthe", songsCount: 1, albumsCount: 1).ExecuteAsync(Page);

        // Assert: the song left without artists should now belong to "(No Artist)", in its "(No Album)"
        await new ValidateSongDetailsFlow("New Religion",
            new(Artists: ["(No Artist)"], Album: "(No Album)", AlbumArtist: "(No Artist)")).ExecuteAsync(Page);
    }
}
