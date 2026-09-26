using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Library;

/// <summary>
/// Verifies that the denormalized album/artist counts (maintained by database triggers)
/// stay correct across every kind of library mutation.
/// </summary>
public class DenormalizedCountsTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly SongsFixture _songs = new();
    private readonly ArtistsFixture _artists = new();
    private readonly AlbumsFixture _albums = new();

    [Fact]
    public async Task ImportSongs_CountsReflectImportedSongs()
    {
        // Import three Amaranthe songs across two albums, plus a song with two artists
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[44]); // Amaranthe - Endlessly from MAXIMALISM (Deluxe Edition)
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[46]); // Amaranthe - Burn With Me from The Nexus
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[47]); // Amaranthe - The Nexus from The Nexus
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[6]); // Faithless, Bebe Rexha - New Religion

        // The shared album should count both of its songs
        await new ValidateAlbumCountsFlow("The Nexus", songsCount: 2).ExecuteAsync(Page);

        // The artist should count all three songs and both albums
        await new ValidateArtistCountsFlow("Amaranthe", songsCount: 3, albumsCount: 2).ExecuteAsync(Page);

        // A featured (non-album) artist should count the song but own no albums
        await new ValidateArtistCountsFlow("Bebe Rexha", songsCount: 1, albumsCount: 0).ExecuteAsync(Page);
    }

    [Fact]
    public async Task EditSongAlbum_MovesCountBetweenAlbums()
    {
        // Start with two songs on the same album
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[46]); // Amaranthe - Burn With Me from The Nexus
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[47]); // Amaranthe - The Nexus from The Nexus

        // Move one song to a new album by the same artist
        await new EditSongFlow("Burn With Me", new(
            Album: "Burn With Me (Single)",
            Artists: ["Amaranthe"],
            AlbumArtist: "Amaranthe"))
            .ExecuteAsync(Page);

        // The old album should lose the song and the new album should gain it
        await new ValidateAlbumCountsFlow("The Nexus", songsCount: 1).ExecuteAsync(Page);
        await new ValidateAlbumCountsFlow("Burn With Me (Single)", songsCount: 1).ExecuteAsync(Page);

        // The artist keeps both songs and now owns one more album
        await new ValidateArtistCountsFlow("Amaranthe", songsCount: 2, albumsCount: 2).ExecuteAsync(Page);
    }

    [Fact]
    public async Task EditSongArtists_UpdatesArtistSongCounts()
    {
        // Start with a single-artist song
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[46]); // Amaranthe - Burn With Me from The Nexus

        // Add a featured artist to the song
        await new EditSongFlow("Burn With Me", new(Artists: ["Amaranthe", "Elize Ryd"])).ExecuteAsync(Page);

        // The new artist should count the song, and the original artist should be unchanged
        await new ValidateArtistCountsFlow("Elize Ryd", songsCount: 1, albumsCount: 0).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Amaranthe", songsCount: 1, albumsCount: 1).ExecuteAsync(Page);

        // Remove the featured artist again
        await new EditSongFlow("Burn With Me", new(Artists: ["Amaranthe"])).ExecuteAsync(Page);

        // The removed artist should no longer count the song
        await new ValidateArtistCountsFlow("Elize Ryd", songsCount: 0, albumsCount: 0).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Amaranthe", songsCount: 1, albumsCount: 1).ExecuteAsync(Page);
    }

    [Fact]
    public async Task DeleteSong_DecrementsAlbumAndArtistCounts()
    {
        // Start with two songs sharing an album and artist
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[46]); // Amaranthe - Burn With Me from The Nexus
        await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[47]); // Amaranthe - The Nexus from The Nexus

        // Delete one of the songs
        await new DeleteSongFlow("Burn With Me").ExecuteAsync(Page);

        // Both the album and the artist should drop to a single song
        await new ValidateAlbumCountsFlow("The Nexus", songsCount: 1).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Amaranthe", songsCount: 1, albumsCount: 1).ExecuteAsync(Page);
    }

    [Fact]
    public async Task CreateAlbum_IncrementsArtistAlbumsCount()
    {
        // Create an artist and then an album for it directly through the API
        var artists = await _artists.SeedAsync(RequestContext, UserId, ["Amaranthe"]);
        await _albums.SeedAsync(RequestContext, UserId, artists, [new("The Nexus", 2012)]);

        // The artist should own the new album even though it has no songs yet
        await new ValidateAlbumCountsFlow("The Nexus", songsCount: 0).ExecuteAsync(Page);
        await new ValidateArtistCountsFlow("Amaranthe", songsCount: 0, albumsCount: 1).ExecuteAsync(Page);
    }
}
