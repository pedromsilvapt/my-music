using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Artists;

/// <summary>
/// Integration tests for merging artists, selected together in the artists list.
/// </summary>
public class ArtistMergeTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly ArtistsFixture _artists = new();
    private readonly SongsFixture _songs = new();

    [Fact]
    public async Task MergeArtists_WithAnAlbumOfItsOwn_ShouldGiveTheAlbumAndItsSongsToTheKeptArtist()
    {
        // Setup: a song by two artists in an album of the first one, and a song by the second one alone
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[57]); // Elton John, Sam Fender - Talk to You
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[58]); // Sam Fender - Little Bit Closer

        // Action: select both artists in the list, and merge the first one into the second
        var summary = await new MergeArtistsFlow("Sam Fender", "Elton John").ExecuteAsync(Page);

        // Assert: the dialog should say what the merge changes: only one song references the merged artist
        summary.ShouldBe([
            "1 song will be updated to reference \"Sam Fender\".",
            "The other artist will be deleted. This action cannot be undone.",
        ]);

        // Assert: the kept artist should have taken the merged artist's album, and perform both songs
        await new ShouldArtistExistFlow("Elton John", shouldExist: false).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Sam Fender", songsCount: 2, albumsCount: 2).ExecuteAsync(Page);
        await new ValidateAlbumArtistFlow("Talk to You", "Sam Fender").ExecuteAsync(Page);

        // Assert: the song both artists performed should name the kept artist only once
        await new ValidateSongDetailsFlow("Talk to You",
            new(Artists: ["Sam Fender"], Album: "Talk to You", AlbumArtist: "Sam Fender")).ExecuteAsync(Page);
    }

    [Fact]
    public async Task MergeArtists_SharingAnAlbumName_ShouldMergeThoseAlbumsAndRecordItInTheSongsHistory()
    {
        // Setup: the same album split between an artist and a misspelled duplicate of it, whose song already has
        // its initial (upload) version recorded
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[46]); // Amaranthe - Burn With Me from The Nexus
        await _songs.SeedAsync(RequestContext, UserId,
            SongsFixture.DefaultSongs[47] with { Artists = ["Amarante"], VersionsCount = 1 }); // Amarante - The Nexus from The Nexus

        // Action: merge the misspelled artist into the right one
        var summary = await new MergeArtistsFlow("Amaranthe", "Amarante").ExecuteAsync(Page);

        // Assert: the dialog should warn that the album both artists have is merged as well
        summary.ShouldBe([
            "1 song will be updated to reference \"Amaranthe\".",
            "1 album has the same name as another one, and will be merged into it.",
            "The other artist will be deleted. This action cannot be undone.",
        ]);

        // Assert: a single artist with a single album should remain, holding both songs
        await new ShouldArtistExistFlow("Amarante", shouldExist: false).ExecuteAsync(Page);
        await new ValidateAlbumsNamedFlow("The Nexus", count: 1).ExecuteAsync(Page);
        await new ValidateAlbumCountsFlow("The Nexus", songsCount: 2).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Amaranthe", songsCount: 2, albumsCount: 1).ExecuteAsync(Page);
        await new ValidateSongDetailsFlow("The Nexus",
            new(Artists: ["Amaranthe"], Album: "The Nexus", AlbumArtist: "Amaranthe")).ExecuteAsync(Page);

        // Assert: the song's history should record the change of album artist as a second version
        await new ValidateSongVersionFlow("The Nexus", versionsCount: 2, new(
            Old: new() { Album = new() { Name = "The Nexus", ArtistName = "Amarante" } },
            New: new() { Album = new() { Name = "The Nexus", ArtistName = "Amaranthe" } }))
            .ExecuteAsync(Page);
    }

    [Fact]
    public async Task MergeArtists_NoArtistIntoAnotherArtist_ShouldBeRejectedBeforeItIsConfirmed()
    {
        // Setup: an artist and "(No Artist)", which holds the songs that have no artist
        await _artists.SeedAsync(RequestContext, UserId, "Queen");
        await _artists.SeedAsync(RequestContext, UserId, "(No Artist)");

        // Action: select both artists, and choose to keep the regular one
        var rejection = await new MergeRejectedArtistsFlow("Queen", "(No Artist)").ExecuteAsync(Page);

        // Assert: the dialog should explain why "(No Artist)" cannot be merged away, without letting it be confirmed
        rejection.ShouldBe("The artist '(No Artist)' cannot be merged into another artist: it holds the songs that have no artist");

        // Assert: after leaving the dialog behind, both artists should still be there
        await Page.ReloadAsync();
        await new ShouldArtistExistFlow("Queen", shouldExist: true).ExecuteAsync(Page);
        await new ShouldArtistExistFlow("(No Artist)", shouldExist: true).ExecuteAsync(Page);
    }
}
