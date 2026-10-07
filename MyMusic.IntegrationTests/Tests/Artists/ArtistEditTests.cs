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

    // Scenario: Renaming an artist from its detail page renames it in its songs and records it in their history
    //   Given an artist with two songs in one album
    //   And one of the songs already has its initial version in its history
    //   When the user renames the artist from its detail page
    //   Then no artist has the old name anymore
    //   And the renamed artist still has its album and both songs
    //   And the song names the renamed artist, as its artist and as its album artist
    //   And the song's history has a second version, recording the rename
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

    // Scenario: Renaming an artist featured in another artist's song only changes that artist's name in the song
    //   Given a song by two artists, in an album of the first one
    //   When the user renames the second artist from its row in the artists list
    //   Then the song names the first artist and the renamed one
    //   And the song keeps its album and its album artist
    //   And the first artist still has its song and its album
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

    // Scenario: Renaming an artist to the name of another artist keeps them as two separate artists
    //   Given an artist with a song
    //   And another artist without any songs
    //   When the user renames the artist without songs to the name of the other one
    //   Then two artists share that name
    //   And the song still belongs to only one of them, keeping its album and album artist
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

    // Scenario: Artists selected together are all renamed in a single edit
    //   Given a song by two artists, in an album of the first one
    //   And a song by a third artist
    //   When the user selects the first two artists in the artists list
    //   And renames each of them in the same editor
    //   Then no artist has either of the old names anymore
    //   And the third artist still has its song and its album
    //   And the song they share names both artists as renamed, the first one also as its album artist
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
