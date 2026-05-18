using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Songs;

// TODO: Missing tests for editing other fields:
// - SongsEdit_ChangeToAlbumFromOtherArtist_ShouldBeForbidden
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

        // Validate Song A has the album and artist
        await new ValidateSongDetailsFlow(songA.Title, new(
            Album: "Existing Album",
            Artists: ["Different Artist"]))
            .ExecuteAsync(Page);

        // Validate the album exists
        await new ShouldAlbumExistFlow("Existing Album").ExecuteAsync(Page);

        // Validate the different artist exists
        await new ShouldArtistExistFlow("Different Artist").ExecuteAsync(Page);

        // Validate Song B is unchanged
        await new ValidateSongDetailsFlow(songB.Title, new(
            Album: "Existing Album",
            Artists: ["Existing Artist"]))
            .ExecuteAsync(Page);
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

        // Navigate to albums page and capture current row count
        var home = new HomePage(Page);
        var albumsPage = await home.Navbar.GoToAlbumsAsync();
        var albumCountBefore = await albumsPage.Collection.GetRowCountAsync();

        // Edit Song A: set same album and same artists as Song B
        await new EditSongFlow(songA.Title, new(
            Album: "Shared Album",
            Artists: ["Shared Artist"],
            AlbumArtist: "Shared Artist"))
            .ExecuteAsync(Page);

        // Validate Song A has the shared album and artist
        await new ValidateSongDetailsFlow(songA.Title, new(
            Album: "Shared Album",
            Artists: ["Shared Artist"]))
            .ExecuteAsync(Page);

        // Validate Song B is unchanged
        await new ValidateSongDetailsFlow(songB.Title, new(
            Album: "Shared Album",
            Artists: ["Shared Artist"]))
            .ExecuteAsync(Page);

        // Navigate to albums page and verify no new album was created (row count same)
        albumsPage = await new HomePage(Page).Navbar.GoToAlbumsAsync();
        var albumCountAfter = await albumsPage.Collection.GetRowCountAsync();
        albumCountAfter.ShouldBe(albumCountBefore, "No new album should be created when reusing an existing album");

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

        // Edit Song A to match Song B's title, album, and artist
        await new EditSongFlow(songA.Title, new(
            Title: "Wicker Woman",
            Album: "Wicker Woman",
            Artists: ["Freya Ridings"]))
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
