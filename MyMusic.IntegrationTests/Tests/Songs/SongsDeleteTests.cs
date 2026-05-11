using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Songs;

/// <summary>
/// Integration tests for the delete songs functionality, verifying cascading
/// behavior for artists and albums when songs are deleted.
/// </summary>
public class SongsDeleteTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private SongsFixture _songs = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _songs = new SongsFixture();
    }

    [Fact]
    public async Task DeleteSong_RemovesOrphanedArtistAndAlbum()
    {
        // Seed two songs with different artists and albums to test orphan removal
        var songA = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[1]); // Dylan - The Alibi (unique artist/album)
        var songB = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[2]); // Freya Ridings - Wicker Woman (unique artist/album)

        // Delete the first song via the UI to trigger cascade behavior
        await new DeleteSongFlow(songA.Title).ExecuteAsync(Page);

        // Verify deleted song no longer exists in the collection
        await new ShouldSongExistFlow(songA.Title, shouldExist: false).ExecuteAsync(Page);

        // Verify remaining song still exists with its artist and album intact
        await new ShouldSongExistFlow(songB.Title, shouldExist: true).ExecuteAsync(Page);
        await new ValidateSongDetailsFlow(songB.Title, new ValidateSongOptions(
            Artists: ["Freya Ridings"],
            Album: "Wicker Woman"
        )).ExecuteAsync(Page);

        // Verify orphaned artist and album are removed (no songs reference them)
        await new ShouldArtistExistFlow("Dylan", shouldExist: false).ExecuteAsync(Page);
        await new ShouldArtistExistFlow("Freya Ridings", shouldExist: true).ExecuteAsync(Page);
        await new ShouldAlbumExistFlow("The Alibi", shouldExist: false).ExecuteAsync(Page);
        await new ShouldAlbumExistFlow("Wicker Woman", shouldExist: true).ExecuteAsync(Page);
    }

    [Fact]
    public async Task DeleteSong_KeepsSharedArtistAndAlbum()
    {
        // Seed two songs with the same artist and album to test shared reference preservation
        var songA = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[46]); // Amaranthe - Burn With Me from The Nexus
        var songB = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[47]); // Amaranthe - The Nexus from The Nexus (same artist/album)

        // Delete the first song via the UI to test that shared references are preserved
        await new DeleteSongFlow(songA.Title).ExecuteAsync(Page);

        // Verify deleted song no longer exists in the collection
        await new ShouldSongExistFlow(songA.Title, shouldExist: false).ExecuteAsync(Page);

        // Verify remaining song still exists with its shared artist and album intact
        await new ShouldSongExistFlow(songB.Title, shouldExist: true).ExecuteAsync(Page);
        await new ValidateSongDetailsFlow(songB.Title, new ValidateSongOptions(
            Artists: ["Amaranthe"],
            Album: "The Nexus"
        )).ExecuteAsync(Page);

        // Verify shared artist and album remain because the second song still references them
        await new ShouldArtistExistFlow("Amaranthe", shouldExist: true).ExecuteAsync(Page);
        await new ShouldAlbumExistFlow("The Nexus", shouldExist: true).ExecuteAsync(Page);
    }

    [Fact]
    public async Task DeleteSong_KeepsSharedArtist_RemovesOrphanedAlbum()
    {
        // Seed two songs with the same artist but different albums to test partial orphan scenarios
        var songA = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[11]); // DAMONA - Girl across the street from Girl across the street
        var songB = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[12]); // DAMONA - God give me a car from God give me a car (same artist, different album)

        // Delete the first song via the UI to trigger partial orphan cleanup
        await new DeleteSongFlow(songA.Title).ExecuteAsync(Page);

        // Verify deleted song no longer exists in the collection
        await new ShouldSongExistFlow(songA.Title, shouldExist: false).ExecuteAsync(Page);

        // Verify remaining song still exists with its shared artist and own album
        await new ShouldSongExistFlow(songB.Title, shouldExist: true).ExecuteAsync(Page);
        await new ValidateSongDetailsFlow(songB.Title, new ValidateSongOptions(
            Artists: ["DAMONA"],
            Album: "God give me a car"
        )).ExecuteAsync(Page);

        // Verify shared artist remains because it's still referenced by the remaining song
        await new ShouldArtistExistFlow("DAMONA", shouldExist: true).ExecuteAsync(Page);

        // Verify orphaned album is removed while the referenced album remains
        await new ShouldAlbumExistFlow("Girl across the street", shouldExist: false).ExecuteAsync(Page);
        await new ShouldAlbumExistFlow("God give me a car", shouldExist: true).ExecuteAsync(Page);
    }

    [Fact]
    public async Task DeleteMultipleSongs_RemovesAllOrphanedEntities()
    {
        // Seed four songs, each with different artist and album, to test bulk delete orphan cleanup
        var songA = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[5]); // Dove Cameron - Sand (unique artist/album)
        var songB = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[8]); // Marisa Liz - Guerra Nuclear (unique artist/album)
        var songC = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[9]); // Camille Lellouche - Je remercie mon ex (unique artist/album)
        var songD = await _songs.SeedAsync(RequestContext, UserId, SongsFixture.DefaultSongs[10]); // Bárbara Bandeira - Marcha (unique artist/album)

        // Delete two songs at once via bulk delete to test batch orphan removal
        await new DeleteSongsBulkFlow(songA.Title, songB.Title).ExecuteAsync(Page);

        // Verify deleted songs no longer exist in the collection
        await new ShouldSongExistFlow(songA.Title, shouldExist: false).ExecuteAsync(Page);
        await new ShouldSongExistFlow(songB.Title, shouldExist: false).ExecuteAsync(Page);

        // Verify remaining songs still exist with their entities intact
        await new ShouldSongExistFlow(songC.Title, shouldExist: true).ExecuteAsync(Page);
        await new ShouldSongExistFlow(songD.Title, shouldExist: true).ExecuteAsync(Page);

        // Verify orphaned artists are removed while referenced artists remain
        await new ShouldArtistExistFlow("Dove Cameron", shouldExist: false).ExecuteAsync(Page);
        await new ShouldArtistExistFlow("Marisa Liz", shouldExist: false).ExecuteAsync(Page);
        await new ShouldArtistExistFlow("Camille Lellouche", shouldExist: true).ExecuteAsync(Page);
        await new ShouldArtistExistFlow("Bárbara Bandeira", shouldExist: true).ExecuteAsync(Page);

        // Verify orphaned albums are removed while referenced albums remain
        await new ShouldAlbumExistFlow("Sand", shouldExist: false).ExecuteAsync(Page);
        await new ShouldAlbumExistFlow("Guerra Nuclear", shouldExist: false).ExecuteAsync(Page);
        await new ShouldAlbumExistFlow("Je remercie mon ex", shouldExist: true).ExecuteAsync(Page);
        await new ShouldAlbumExistFlow("Marcha", shouldExist: true).ExecuteAsync(Page);
    }
}
