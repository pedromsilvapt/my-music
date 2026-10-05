using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Albums;

/// <summary>
/// Integration tests for deleting an album, from the albums list and from the album's detail page.
/// </summary>
public class AlbumDeleteTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly ArtistsFixture _artists = new();
    private readonly AlbumsFixture _albums = new();
    private readonly SongsFixture _songs = new();

    [Fact]
    public async Task DeleteAlbum_WithoutSongs_ShouldDeleteItWithoutAWarning()
    {
        // Setup: an artist with two empty albums
        var queen = await _artists.SeedAsync(RequestContext, UserId, "Queen");
        await _albums.SeedAsync(RequestContext, UserId, [queen], [new SampleAlbum("Jazz", 1978), new SampleAlbum("Innuendo", 1991)]);

        // Action: delete one of the albums from its row in the albums list
        var warning = await new DeleteAlbumFlow("Jazz").ExecuteAsync(Page);

        // Assert: no song references the album, so the confirmation should not warn about any
        warning.ShouldBeNull();

        // Assert: only that album should be gone, and its artist should stay with the other one
        await new ShouldAlbumExistFlow("Jazz", shouldExist: false).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Queen", songsCount: 0, albumsCount: 1).ExecuteAsync(Page);
    }

    [Fact]
    public async Task DeleteAlbum_WithSongs_ShouldWarnAndMoveItsSongsToNoAlbum()
    {
        // Setup: an album with two songs
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[46]); // Amaranthe - Burn With Me from The Nexus
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[47]); // Amaranthe - The Nexus from The Nexus

        // Action: delete the album from its detail page
        var warning = await new DeleteAlbumFlow("The Nexus", fromDetailsPage: true).ExecuteAsync(Page);

        // Assert: the confirmation should warn about the songs that still reference the album
        warning.ShouldBe("2 songs still reference this album. They will be moved to \"(No Album)\".");

        // Assert: the album should be gone, replaced by the "(No Album)" of the same album artist holding both songs
        await new ShouldAlbumExistFlow("The Nexus", shouldExist: false).ExecuteAsync(Page);
        await new ValidateAlbumCountsFlow("(No Album)", songsCount: 2).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Amaranthe", songsCount: 2, albumsCount: 1).ExecuteAsync(Page);

        // Assert: each song should keep its artist, and say it has no album
        await new ValidateSongDetailsFlow("Burn With Me",
            new(Artists: ["Amaranthe"], Album: "(No Album)", AlbumArtist: "Amaranthe")).ExecuteAsync(Page);
    }

    [Fact]
    public async Task DeleteAlbums_SelectedTogether_ShouldWarnOnceAndDeleteThemAll()
    {
        // Setup: two albums with one song each, an empty album, and an album to keep
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[44]); // Amaranthe - Endlessly from MAXIMALISM (Deluxe Edition)
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[45]); // Amaranthe - Helix from HELIX
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[46]); // Amaranthe - Burn With Me from The Nexus
        var queen = await _artists.SeedAsync(RequestContext, UserId, "Queen");
        await _albums.SeedAsync(RequestContext, UserId, [queen], [new SampleAlbum("Jazz", 1978)]);

        // Action: select three of the albums in the list and delete them through the selection's actions
        var warning = await new DeleteAlbumsBulkFlow("MAXIMALISM (Deluxe Edition)", "HELIX", "Jazz").ExecuteAsync(Page);

        // Assert: a single confirmation should warn about the songs of all the selected albums together
        warning.ShouldBe("2 songs still reference these albums. They will be moved to \"(No Album)\".");

        // Assert: the selected albums should be gone, and the one left out should still have its song
        await new ShouldAlbumExistFlow("HELIX", shouldExist: false).ExecuteAsync(Page);
        await new ShouldAlbumExistFlow("Jazz", shouldExist: false).ExecuteAsync(Page);
        await new ValidateAlbumCountsFlow("The Nexus", songsCount: 1).ExecuteAsync(Page);

        // Assert: both songs should now share the "(No Album)" of their album artist
        await new ValidateAlbumCountsFlow("(No Album)", songsCount: 2).ExecuteAsync(Page);
        await new ValidateSongDetailsFlow("Helix", new(Album: "(No Album)", AlbumArtist: "Amaranthe")).ExecuteAsync(Page);
    }
}
