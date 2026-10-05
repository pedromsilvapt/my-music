using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Albums;

/// <summary>
/// Integration tests for merging albums, selected together in the albums list.
/// </summary>
public class AlbumMergeTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly ArtistsFixture _artists = new();
    private readonly AlbumsFixture _albums = new();
    private readonly SongsFixture _songs = new();

    [Fact]
    public async Task MergeAlbums_OfTheSameArtist_ShouldMoveTheirSongsToTheKeptAlbumAndRecordItInTheirHistory()
    {
        // Setup: three albums of one artist, one of whose songs already has its initial (upload) version recorded
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[44]); // Amaranthe - Endlessly from MAXIMALISM (Deluxe Edition)
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[45] with { VersionsCount = 1 }); // Amaranthe - Helix from HELIX
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[46]); // Amaranthe - Burn With Me from The Nexus

        // Action: select the three albums in the list, and merge the other two into one of them
        var summary = await new MergeAlbumsFlow("The Nexus", "HELIX", "MAXIMALISM (Deluxe Edition)").ExecuteAsync(Page);

        // Assert: the dialog should say what the merge changes: the songs already have the album's artist
        summary.ShouldBe([
            "2 songs will be moved to \"The Nexus\" by Amaranthe.",
            "The other 2 albums will be deleted. This action cannot be undone.",
        ]);

        // Assert: only the kept album should remain, with the songs of all three
        await new ShouldAlbumExistFlow("HELIX", shouldExist: false).ExecuteAsync(Page);
        await new ShouldAlbumExistFlow("MAXIMALISM (Deluxe Edition)", shouldExist: false).ExecuteAsync(Page);
        await new ValidateAlbumCountsFlow("The Nexus", songsCount: 3).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Amaranthe", songsCount: 3, albumsCount: 1).ExecuteAsync(Page);

        // Assert: a merged song should name the kept album
        await new ValidateSongDetailsFlow("Endlessly",
            new(Artists: ["Amaranthe"], Album: "The Nexus", AlbumArtist: "Amaranthe")).ExecuteAsync(Page);

        // Assert: the song's history should record the move as a second version
        await new ValidateSongVersionFlow("Helix", versionsCount: 2, new(
            Old: new() { Album = new() { Name = "HELIX", ArtistName = "Amaranthe" } },
            New: new() { Album = new() { Name = "The Nexus", ArtistName = "Amaranthe" } }))
            .ExecuteAsync(Page);
    }

    [Fact]
    public async Task MergeAlbums_OfDifferentArtists_ShouldWarnAndAddTheKeptAlbumsArtistToTheSongsLackingIt()
    {
        // Setup: an album of one artist, and an album of another artist whose song the first one does not perform
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[57]); // Elton John, Sam Fender - Talk to You
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[58]); // Sam Fender - Little Bit Closer

        // Action: merge the second artist's album into the first one's
        var summary = await new MergeAlbumsFlow("Talk to You", "Little Bit Closer").ExecuteAsync(Page);

        // Assert: the dialog should warn that the moved song gains the album artist of the kept album
        summary.ShouldBe([
            "1 song will be moved to \"Talk to You\" by Elton John.",
            "1 of them does not have the artist Elton John yet, and will gain it.",
            "The other album will be deleted. This action cannot be undone.",
        ]);

        // Assert: the moved song should be in the kept album, performed by its own artist and the album's
        await new ShouldAlbumExistFlow("Little Bit Closer", shouldExist: false).ExecuteAsync(Page);
        await new ValidateAlbumCountsFlow("Talk to You", songsCount: 2).ExecuteAsync(Page);
        await new ValidateSongDetailsFlow("Little Bit Closer",
            new(Artists: ["Sam Fender", "Elton John"], Album: "Talk to You", AlbumArtist: "Elton John")).ExecuteAsync(Page);

        // Assert: the artist that lost its album should still perform both songs
        await new ValidateArtistCountsFlow("Sam Fender", songsCount: 2, albumsCount: 0).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Elton John", songsCount: 2, albumsCount: 1).ExecuteAsync(Page);
    }

    [Fact]
    public async Task MergeAlbums_NoAlbumIntoAnotherAlbum_ShouldBeRejectedBeforeItIsConfirmed()
    {
        // Setup: an artist with an album and its "(No Album)", which holds the songs that have no album
        var queen = await _artists.SeedAsync(RequestContext, UserId, "Queen");
        await _albums.SeedAsync(RequestContext, UserId, [queen], [new SampleAlbum("Jazz", 1978), new SampleAlbum("(No Album)", null)]);

        // Action: select both albums, and choose to keep the regular one
        var rejection = await new MergeRejectedAlbumsFlow("Jazz", "(No Album)").ExecuteAsync(Page);

        // Assert: the dialog should explain why "(No Album)" cannot be merged away, without letting it be confirmed
        rejection.ShouldBe("The album '(No Album)' of 'Queen' cannot be merged into another album: it holds the songs that have no album");

        // Assert: after leaving the dialog behind, both albums should still be there
        await Page.ReloadAsync();
        await new ValidateAlbumsNamedFlow("Jazz", count: 1).ExecuteAsync(Page);
        await new ValidateAlbumsNamedFlow("(No Album)", count: 1).ExecuteAsync(Page);
    }
}
