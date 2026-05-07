using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Songs;

// TODO: Missing tests for editing other fields:
// - SongsEdit_AddExistingArtist (add one artist that already exists in the database)
// - SongsEdit_ChangeToExistingAlbum
// - SongsEdit_ChangeToNewAlbum
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
}
