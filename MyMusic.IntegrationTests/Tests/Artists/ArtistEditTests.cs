using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;

namespace MyMusic.IntegrationTests.Tests.Artists;

/// <summary>
/// Integration tests for renaming artists, from the artists list and from an artist's detail page.
/// </summary>
public class ArtistEditTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly ArtistsFixture _artists = new();
    private readonly SongsFixture _songs = new();

    [Fact]
    public async Task EditArtist_RenamedFromItsDetailsPage_ShouldRenameItInItsSongsAndRecordItInTheirHistory()
    {
        // Setup: an artist with two songs in one album, one of which already has its initial (upload) version recorded
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[46] with { VersionsCount = 1 }); // Amaranthe - Burn With Me from The Nexus
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[47]); // Amaranthe - The Nexus from The Nexus

        // Action: rename the artist from its detail page
        await new EditArtistFlow("Amaranthe", "Amaranthe (SE)", fromDetailsPage: true).ExecuteAsync(Page);

        // Assert: the artist should be renamed in place, keeping its album and both songs
        await new ValidateArtistsNamedFlow("Amaranthe", count: 0).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Amaranthe (SE)", songsCount: 2, albumsCount: 1).ExecuteAsync(Page);

        // Assert: each song should name the renamed artist, as its artist and as its album artist
        await new ValidateSongDetailsFlow("Burn With Me",
            new(Artists: ["Amaranthe (SE)"], Album: "The Nexus", AlbumArtist: "Amaranthe (SE)")).ExecuteAsync(Page);

        // Assert: the song's history should record the rename as a second version
        await new ValidateSongVersionFlow("Burn With Me", versionsCount: 2, new(
            Old: new() { Artists = [new() { Name = "Amaranthe" }] },
            New: new() { Artists = [new() { Name = "Amaranthe (SE)" }] }))
            .ExecuteAsync(Page);
    }

    [Fact]
    public async Task EditArtist_FeaturedInAnotherArtistsSongRenamedFromItsRow_ShouldOnlyRenameItInTheSong()
    {
        // Setup: a song by two artists, in an album of the first one
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[6]); // Faithless, Bebe Rexha - New Religion

        // Action: rename the featured artist from its row in the artists list
        await new EditArtistFlow("Bebe Rexha", "Bebe").ExecuteAsync(Page);

        // Assert: the song should keep its album and album artist, naming the featured artist as renamed
        await new ValidateSongDetailsFlow("New Religion",
            new(Artists: ["Faithless", "Bebe"], Album: "New Religion", AlbumArtist: "Faithless")).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Faithless", songsCount: 1, albumsCount: 1).ExecuteAsync(Page);
    }

    [Fact]
    public async Task EditArtist_RenamedToTheNameOfAnotherArtist_ShouldKeepBothArtists()
    {
        // Setup: an artist with a song, and another artist without any
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[46]); // Amaranthe - Burn With Me from The Nexus
        await _artists.SeedAsync(RequestContext, UserId, "Queen");

        // Action: give the empty artist the name of the other one — artist names are not unique, so this is no merge
        await new EditArtistFlow("Queen", "Amaranthe").ExecuteAsync(Page);

        // Assert: two artists should now share the name, and the song should still belong to only one of them
        await new ValidateArtistsNamedFlow("Amaranthe", count: 2).ExecuteAsync(Page);
        await new ValidateSongDetailsFlow("Burn With Me",
            new(Artists: ["Amaranthe"], Album: "The Nexus", AlbumArtist: "Amaranthe")).ExecuteAsync(Page);
    }

    [Fact]
    public async Task EditArtists_SelectedTogether_ShouldRenameThemAllAtOnce()
    {
        // Setup: a song by two artists, and a song by a third one to leave alone
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[6]); // Faithless, Bebe Rexha - New Religion
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[46]); // Amaranthe - Burn With Me from The Nexus

        // Action: select both artists of the first song, and rename them one after the other in a single editor
        await new EditArtistsBulkFlow(new()
        {
            ["Faithless"] = "Faithless (UK)",
            ["Bebe Rexha"] = "Bebe",
        }).ExecuteAsync(Page);

        // Assert: both artists should be renamed, and the one left out should be untouched
        await new ValidateArtistsNamedFlow("Faithless", count: 0).ExecuteAsync(Page);
        await new ValidateArtistsNamedFlow("Bebe Rexha", count: 0).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Amaranthe", songsCount: 1, albumsCount: 1).ExecuteAsync(Page);

        // Assert: the song they share should name both as renamed
        await new ValidateSongDetailsFlow("New Religion",
            new(Artists: ["Faithless (UK)", "Bebe"], Album: "New Religion", AlbumArtist: "Faithless (UK)")).ExecuteAsync(Page);
    }
}
