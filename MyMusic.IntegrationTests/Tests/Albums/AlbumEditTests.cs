using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Albums;

/// <summary>
/// Integration tests for editing albums, from the albums list and from an album's detail page.
/// </summary>
public class AlbumEditTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly ArtistsFixture _artists = new();
    private readonly AlbumsFixture _albums = new();
    private readonly SongsFixture _songs = new();

    [Fact]
    public async Task EditAlbum_RenamedFromItsDetailsPage_ShouldRenameItInItsSongsAndRecordItInTheirHistory()
    {
        // Setup: an album with two songs, one of which already has its initial (upload) version recorded
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[46] with { VersionsCount = 1 }); // Amaranthe - Burn With Me from The Nexus
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[47]); // Amaranthe - The Nexus from The Nexus

        // Action: rename the album from its detail page
        await new EditAlbumFlow("The Nexus", new(Name: "The Nexus (Remastered)"), fromDetailsPage: true).ExecuteAsync(Page);

        // Assert: the album should be renamed in place, keeping both songs and staying its artist's only album
        await new ValidateAlbumsNamedFlow("The Nexus", count: 0).ExecuteAsync(Page);
        await new ValidateAlbumCountsFlow("The Nexus (Remastered)", songsCount: 2).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Amaranthe", songsCount: 2, albumsCount: 1).ExecuteAsync(Page);

        // Assert: each song should name the renamed album
        await new ValidateSongDetailsFlow("Burn With Me",
            new(Artists: ["Amaranthe"], Album: "The Nexus (Remastered)", AlbumArtist: "Amaranthe")).ExecuteAsync(Page);

        // Assert: the song's history should record the rename as a second version
        await new ValidateSongVersionFlow("Burn With Me", versionsCount: 2, new(
            Old: new() { Album = new() { Name = "The Nexus", ArtistName = "Amaranthe" } },
            New: new() { Album = new() { Name = "The Nexus (Remastered)", ArtistName = "Amaranthe" } }))
            .ExecuteAsync(Page);
    }

    [Fact]
    public async Task EditAlbum_NameAndYearChangedFromItsRow_ShouldShowBothWithoutAReload()
    {
        // Setup: an artist with two empty albums
        var queen = await _artists.SeedAsync(RequestContext, UserId, "Queen");
        await _albums.SeedAsync(RequestContext, UserId, [queen], [new SampleAlbum("Jazz", 1978), new SampleAlbum("Innuendo", 1991)]);

        // Action: rename one of the albums and change its year, from its row in the albums list
        await new EditAlbumFlow("Jazz", new(Name: "Jazz (Remastered)", Year: 2011)).ExecuteAsync(Page);

        // Assert: only that album should have changed, and it should still be one of its artist's two albums
        await new ValidateAlbumsNamedFlow("Jazz", count: 0).ExecuteAsync(Page);
        await new ValidateAlbumYearFlow("Jazz (Remastered)", 2011).ExecuteAsync(Page);
        await new ValidateAlbumYearFlow("Innuendo", 1991).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Queen", songsCount: 0, albumsCount: 2).ExecuteAsync(Page);
    }

    [Fact]
    public async Task EditAlbum_RenamedToANameItsArtistAlreadyHas_ShouldBeRejectedPointingToMerge()
    {
        // Setup: an artist with two albums
        var queen = await _artists.SeedAsync(RequestContext, UserId, "Queen");
        await _albums.SeedAsync(RequestContext, UserId, [queen], [new SampleAlbum("Jazz", 1978), new SampleAlbum("Innuendo", 1991)]);

        // Action: try to give one of the albums the name of the other
        var error = await new EditRejectedAlbumFlow("Jazz", "Innuendo").ExecuteAsync(Page);

        // Assert: the editor should stay open explaining why, and that the two albums are to be merged instead
        error.ShouldBe("Artist 'Queen' already has an album named 'Innuendo'. To join the two albums, merge them instead.");

        // Assert: after leaving the editor behind, both albums should be as they were
        await Page.ReloadAsync();
        await new ValidateAlbumsNamedFlow("Jazz", count: 1).ExecuteAsync(Page);
        await new ValidateAlbumsNamedFlow("Innuendo", count: 1).ExecuteAsync(Page);
    }

    [Fact]
    public async Task EditAlbums_SelectedTogether_ShouldSaveThemAllAtOnce()
    {
        // Setup: two albums with one song each, and an album to leave alone
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[44]); // Amaranthe - Endlessly from MAXIMALISM (Deluxe Edition)
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[45]); // Amaranthe - Helix from HELIX
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[46]); // Amaranthe - Burn With Me from The Nexus

        // Action: select two of the albums in the list, and edit them one after the other in a single editor
        await new EditAlbumsBulkFlow(new()
        {
            ["MAXIMALISM (Deluxe Edition)"] = new(Name: "MAXIMALISM"),
            ["HELIX"] = new(Name: "Helix (Album)", Year: 2019),
        }).ExecuteAsync(Page);

        // Assert: both albums should carry their changes, and the one left out should be untouched
        await new ValidateAlbumsNamedFlow("MAXIMALISM (Deluxe Edition)", count: 0).ExecuteAsync(Page);
        await new ValidateAlbumsNamedFlow("HELIX", count: 0).ExecuteAsync(Page);
        await new ValidateAlbumYearFlow("Helix (Album)", 2019).ExecuteAsync(Page);
        await new ValidateAlbumCountsFlow("The Nexus", songsCount: 1).ExecuteAsync(Page);

        // Assert: the songs of both albums should name them as renamed
        await new ValidateSongDetailsFlow("Endlessly", new(Album: "MAXIMALISM", AlbumArtist: "Amaranthe")).ExecuteAsync(Page);
        await new ValidateSongDetailsFlow("Helix", new(Album: "Helix (Album)", AlbumArtist: "Amaranthe")).ExecuteAsync(Page);
    }
}
