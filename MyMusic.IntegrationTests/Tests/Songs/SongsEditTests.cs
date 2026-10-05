using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Songs;

// TODO: Missing tests for editing other fields:
// - SongsEdit_ChangeYear
// - SongsEdit_ChangeExplicit
// - SongsEdit_ChangeGenres
// - SongsEdit_ChangeMultipleFields (validate that multiple field changes work correctly)

public class SongsEditTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private SongsFixture _songs = null!;
    private DevicesFixture _devices = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _songs = new SongsFixture();
        _devices = new DevicesFixture();
    }

    [Fact]
    public async Task SongsEdit_AddNewArtist()
    {
        // Create a device for this test
        var device = await _devices.SeedAsync(RequestContext, UserId, DevicesFixture.DefaultDevices[0]);

        // Seed a song associated with the device
        var sampleSong = SongsFixture.DefaultSongs[0] with { DeviceIds = [device.Id] };
        var song = await _songs.SeedAsync(RequestContext, UserId, sampleSong);

        // Open the song details page
        var songDetails = await new OpenSongDetailsFlow(song.Title).ExecuteAsync(Page);

        // Verify the song is on the device before edit
        var hasDeviceBefore = await songDetails.HasDeviceAsync(device.Name);
        hasDeviceBefore.ShouldBeTrue();

        // Change the artist to a brand new artist name
        await new EditSongFlow(song.Title, new(Artists: ["U2", "Dylan"])).ExecuteAsync(Page);

        // Validate the artist changed on the UI
        await new ValidateSongDetailsFlow(song.Title, new(Artists: ["U2", "Dylan"]))
            .ExecuteAsync(Page);

        // Validate the device is marked for download (proves checksum changed)
        await new ShouldSongExistInDeviceFlow(
            song.Title,
            device.Name,
            shouldExist: true,
            syncAction: "Download")
            .ExecuteAsync(Page);
    }

    [Fact]
    public async Task EditSong_CreateAllEntitiesFromNoMetadata()
    {
        // Seed a device
        var device = await _devices.SeedAsync(RequestContext, UserId, DevicesFixture.DefaultDevices[0]);

        // Seed a song with title only (no album, artists, or album artist)
        var songA = await _songs.SeedAsync(RequestContext, UserId,
            new SampleSong(Title: "No Metadata Song", Artists: null, Album: null, AlbumArtist: null, DeviceIds: [device.Id]));

        // Edit the song: set title, album, album artist, and artist
        await new EditSongFlow(songA.Title, new(
            Title: "Brand New Title",
            Album: "Brand New Album",
            AlbumArtist: "Brand New Artist",
            Artists: ["Brand New Artist"]))
            .ExecuteAsync(Page);

        // Validate song details show the new values
        await new ValidateSongDetailsFlow("Brand New Title", new(
            Title: "Brand New Title",
            Album: "Brand New Album",
            Artists: ["Brand New Artist"]))
            .ExecuteAsync(Page);

        // Validate the album was created
        await new ShouldAlbumExistFlow("Brand New Album").ExecuteAsync(Page);

        // Validate the artist was created
        await new ShouldArtistExistFlow("Brand New Artist").ExecuteAsync(Page);
    }

    [Fact]
    public async Task EditSong_SameAlbumDifferentArtistCreatesNewAlbum()
    {
        // Seed a device
        var device = await _devices.SeedAsync(RequestContext, UserId, DevicesFixture.DefaultDevices[0]);

        // Seed Song A with no album
        var songA = await _songs.SeedAsync(RequestContext, UserId,
            new SampleSong(Title: "Song A", Artists: null, Album: null, DeviceIds: [device.Id]));

        // Seed Song B with album and artist
        var songB = await _songs.SeedAsync(RequestContext, UserId,
            new SampleSong(Title: "Song B", Album: "Existing Album", Artists: ["Existing Artist"], AlbumArtist: "Existing Artist", DeviceIds: [device.Id]));

        // Edit Song A: set same album name but different artist and album artist
        await new EditSongFlow(songA.Title, new(
            Album: "Existing Album",
            Artists: ["Different Artist"],
            AlbumArtist: "Different Artist"))
            .ExecuteAsync(Page);

        // Song A should be on an album of its own album artist, despite the shared name
        await new ValidateSongDetailsFlow(songA.Title, new(
            Album: "Existing Album",
            Artists: ["Different Artist"],
            AlbumArtist: "Different Artist"))
            .ExecuteAsync(Page);

        // Both artists should now have an album with that name
        await new ValidateAlbumsNamedFlow("Existing Album", count: 2).ExecuteAsync(Page);

        // Validate the different artist exists
        await new ShouldArtistExistFlow("Different Artist").ExecuteAsync(Page);

        // Song B should still be on the existing artist's album
        await new ValidateSongDetailsFlow(songB.Title, new(
            Album: "Existing Album",
            Artists: ["Existing Artist"],
            AlbumArtist: "Existing Artist"))
            .ExecuteAsync(Page);
    }

    [Fact]
    public async Task EditSong_PickAlbumOfAnotherArtistThenChangeAlbumArtist_CreatesAlbumForThatArtist()
    {
        // Seed a device
        var device = await _devices.SeedAsync(RequestContext, UserId, DevicesFixture.DefaultDevices[0]);

        // Seed Song A on Artist A's album
        var songA = await _songs.SeedAsync(RequestContext, UserId,
            new SampleSong(Title: "Song A", Album: "Album A", Artists: ["Artist A"], AlbumArtist: "Artist A", DeviceIds: [device.Id]));

        // Seed Song B on Artist B's own album
        var songB = await _songs.SeedAsync(RequestContext, UserId,
            new SampleSong(Title: "Song B", Album: "Album B", Artists: ["Artist B"], AlbumArtist: "Artist B", DeviceIds: [device.Id]));

        // Edit Song B: pick Artist A's album among the suggestions, which fills in Artist A as the album artist,
        // then set the album artist back to Artist B
        await new EditSongFlow(songB.Title, new(
            Album: "Album A",
            AlbumSuggestionOf: "Artist A",
            AlbumArtist: "Artist B"))
            .ExecuteAsync(Page);

        // Song B should be on an "Album A" of Artist B, not on Artist A's album
        await new ValidateSongDetailsFlow(songB.Title, new(
            Album: "Album A",
            Artists: ["Artist B"],
            AlbumArtist: "Artist B"))
            .ExecuteAsync(Page);

        // Song A should still be on Artist A's album
        await new ValidateSongDetailsFlow(songA.Title, new(
            Album: "Album A",
            Artists: ["Artist A"],
            AlbumArtist: "Artist A"))
            .ExecuteAsync(Page);

        // Each artist should have its own "Album A", and the album Song B left empty should be gone
        await new ValidateAlbumsNamedFlow("Album A", count: 2).ExecuteAsync(Page);
        await new ShouldAlbumExistFlow("Album B", shouldExist: false).ExecuteAsync(Page);
    }

    [Fact]
    public async Task EditSong_PickAlbumSuggestion_FillsInItsAlbumArtist()
    {
        // Seed a device
        var device = await _devices.SeedAsync(RequestContext, UserId, DevicesFixture.DefaultDevices[0]);

        // Seed Song A with no album
        var songA = await _songs.SeedAsync(RequestContext, UserId,
            new SampleSong(Title: "Song A", Artists: null, Album: null, DeviceIds: [device.Id]));

        // Seed Song B with album and artist
        await _songs.SeedAsync(RequestContext, UserId,
            new SampleSong(Title: "Song B", Album: "Existing Album", Artists: ["Existing Artist"], AlbumArtist: "Existing Artist", DeviceIds: [device.Id]));

        // Edit Song A: pick the existing album among the suggestions, without touching the album artist
        await new EditSongFlow(songA.Title, new(
            Album: "Existing Album",
            AlbumSuggestionOf: "Existing Artist",
            Artists: ["Existing Artist"]))
            .ExecuteAsync(Page);

        // Song A should have joined the existing album, whose artist was filled in by the suggestion
        await new ValidateSongDetailsFlow(songA.Title, new(
            Album: "Existing Album",
            Artists: ["Existing Artist"],
            AlbumArtist: "Existing Artist"))
            .ExecuteAsync(Page);

        // No second album should have been created
        await new ValidateAlbumsNamedFlow("Existing Album", count: 1).ExecuteAsync(Page);
    }

    [Fact]
    public async Task EditSong_ChangeAlbumArtistOnly_MovesSongToThatArtistsAlbum()
    {
        // Seed a device
        var device = await _devices.SeedAsync(RequestContext, UserId, DevicesFixture.DefaultDevices[0]);

        // Seed two songs on Artist A's album, one of them featuring Artist B
        var songA = await _songs.SeedAsync(RequestContext, UserId,
            new SampleSong(Title: "Song A", Album: "Shared Album", Artists: ["Artist A"], AlbumArtist: "Artist A", DeviceIds: [device.Id]));
        var songB = await _songs.SeedAsync(RequestContext, UserId,
            new SampleSong(Title: "Song B", Album: "Shared Album", Artists: ["Artist A", "Artist B"], AlbumArtist: "Artist A", DeviceIds: [device.Id]));

        // Edit Song B: change nothing but the album artist
        await new EditSongFlow(songB.Title, new(AlbumArtist: "Artist B")).ExecuteAsync(Page);

        // Song B should have moved to an album of the same name by Artist B
        await new ValidateSongDetailsFlow(songB.Title, new(
            Album: "Shared Album",
            AlbumArtist: "Artist B"))
            .ExecuteAsync(Page);

        // Song A should have stayed on Artist A's album
        await new ValidateSongDetailsFlow(songA.Title, new(
            Album: "Shared Album",
            AlbumArtist: "Artist A"))
            .ExecuteAsync(Page);

        await new ValidateAlbumsNamedFlow("Shared Album", count: 2).ExecuteAsync(Page);
    }

    [Fact]
    public async Task EditSong_SameAlbumAndArtistReusesExistingAlbum()
    {
        // Seed a device
        var device = await _devices.SeedAsync(RequestContext, UserId, DevicesFixture.DefaultDevices[0]);

        // Seed Song A with no album
        var songA = await _songs.SeedAsync(RequestContext, UserId,
            new SampleSong(Title: "Song A", Artists: null, Album: null, DeviceIds: [device.Id]));

        // Seed Song B with album and artist
        var songB = await _songs.SeedAsync(RequestContext, UserId,
            new SampleSong(Title: "Song B", Album: "Shared Album", Artists: ["Shared Artist"], AlbumArtist: "Shared Artist", DeviceIds: [device.Id]));

        // Edit Song A: set same album and same artists as Song B
        await new EditSongFlow(songA.Title, new(
            Album: "Shared Album",
            Artists: ["Shared Artist"],
            AlbumArtist: "Shared Artist"))
            .ExecuteAsync(Page);

        // Validate Song A has the shared album and artist
        await new ValidateSongDetailsFlow(songA.Title, new(
            Album: "Shared Album",
            Artists: ["Shared Artist"],
            AlbumArtist: "Shared Artist"))
            .ExecuteAsync(Page);

        // Validate Song B is unchanged
        await new ValidateSongDetailsFlow(songB.Title, new(
            Album: "Shared Album",
            Artists: ["Shared Artist"]))
            .ExecuteAsync(Page);

        // The existing album should have been reused, not duplicated
        await new ValidateAlbumsNamedFlow("Shared Album", count: 1).ExecuteAsync(Page);

        // Validate the shared artist exists (no duplicate created)
        await new ShouldArtistExistFlow("Shared Artist").ExecuteAsync(Page);
    }

    [Fact]
    public async Task EditSong_SameArtistDifferentAlbumCreatesNewAlbum()
    {
        // Seed a device
        var device = await _devices.SeedAsync(RequestContext, UserId, DevicesFixture.DefaultDevices[0]);

        // Seed Song A with no album
        var songA = await _songs.SeedAsync(RequestContext, UserId,
            new SampleSong(Title: "Song A", Artists: null, Album: null, DeviceIds: [device.Id]));

        // Seed Song B with album and artist
        var songB = await _songs.SeedAsync(RequestContext, UserId,
            new SampleSong(Title: "Song B", Album: "Existing Album", Artists: ["Existing Artist"], AlbumArtist: "Existing Artist", DeviceIds: [device.Id]));

        // Edit Song A: set different album, but same artist
        await new EditSongFlow(songA.Title, new(
            Album: "New Album",
            Artists: ["Existing Artist"],
            AlbumArtist: "Existing Artist"))
            .ExecuteAsync(Page);

        // Validate Song A has the new album and existing artist
        await new ValidateSongDetailsFlow(songA.Title, new(
            Album: "New Album",
            Artists: ["Existing Artist"]))
            .ExecuteAsync(Page);

        // Validate the new album was created
        await new ShouldAlbumExistFlow("New Album").ExecuteAsync(Page);

        // Validate the existing album still exists
        await new ShouldAlbumExistFlow("Existing Album").ExecuteAsync(Page);

        // Validate the existing artist exists (reused, not duplicated)
        await new ShouldArtistExistFlow("Existing Artist").ExecuteAsync(Page);
    }

    [Fact]
    public async Task EditSong_DuplicateTitleAlbumArtistRenamesFilePath()
    {
        // Seed a device
        var device = await _devices.SeedAsync(RequestContext, UserId, DevicesFixture.DefaultDevices[0]);

        // Seed Song A with no album
        var songA = await _songs.SeedAsync(RequestContext, UserId,
            new SampleSong(Title: "Song A", Artists: null, Album: null, DeviceIds: [device.Id]));

        // Seed Song B with full data (same title and album name)
        var songB = await _songs.SeedAsync(RequestContext, UserId,
            new SampleSong(Title: "Wicker Woman", Album: "Wicker Woman", Artists: ["Freya Ridings"], DeviceIds: [device.Id]));

        // Navigate to Song B details to capture its repository path
        var songBDetails = await new OpenSongDetailsFlow(songB.Title).ExecuteAsync(Page);
        var songBPath = await songBDetails.GetRepositoryPathAsync();

        // Edit Song A to match Song B's title, album, artist and album artist
        await new EditSongFlow(songA.Title, new(
            Title: "Wicker Woman",
            Album: "Wicker Woman",
            Artists: ["Freya Ridings"],
            AlbumArtist: "Freya Ridings"))
            .ExecuteAsync(Page);

        // Navigate to Song A's details using row index (both songs now have same title)
        await new ValidateSongDetailsFlow(0, new(
            Title: "Wicker Woman",
            Artists: ["Freya Ridings"],
            Album: "Wicker Woman"))
            .ExecuteAsync(Page);

        // Verify Song A's repository path has the counter suffix
        var songADetails = await new OpenSongDetailsFlow(0).ExecuteAsync(Page);
        var songAPath = await songADetails.GetRepositoryPathAsync();

        var expectedPath = songBPath!.Replace(".mp3", " (2).mp3");
        songAPath.ShouldBe(expectedPath, "Song A's file path should have ' (2)' before the extension");
    }
}
