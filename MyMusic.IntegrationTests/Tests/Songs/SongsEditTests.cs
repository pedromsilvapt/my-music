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

    // Scenario: Changing a song's artists marks it for download on its devices
    //   Given a song that is on a device
    //   When the user changes the song's artists to ones that do not exist yet
    //   Then the song shows the new artists
    //   And the song is marked for download on the device
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

    // Scenario: Filling in a song that has no metadata creates its album and artist
    //   Given a song with only a title, without album, artists or album artist
    //   When the user sets its title, album, album artist and artist
    //   Then the song shows the new title, album and artist
    //   And the album exists
    //   And the artist exists
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

    // Scenario: Using an existing album's name with a different album artist creates a new album
    //   Given a song without an album
    //   And another song on an existing artist's album
    //   When the user sets the first song's album to that album's name, with a different artist and album artist
    //   Then the first song is on an album of its own album artist
    //   And there are two albums with that name
    //   And the different artist exists
    //   And the other song is still on the existing artist's album
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

    // Scenario: Picking another artist's album and then changing the album artist creates an album for that artist
    //   Given a song on an album of one artist
    //   And another song on an album of its own, by a second artist
    //   When the user edits the second song, picking the first artist's album among the suggestions
    //   And sets the album artist back to the second artist
    //   Then the second song is on an album with that name by the second artist
    //   And the first song is still on the first artist's album
    //   And each artist has its own album with that name
    //   And the album the second song left empty no longer exists
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

    // Scenario: Picking an album among the suggestions fills in its album artist
    //   Given a song without an album
    //   And another song on an existing artist's album
    //   When the user edits the first song, picking the existing album among the suggestions
    //   And sets its artist, without touching the album artist
    //   Then the first song is on the existing album, with that album's artist as its album artist
    //   And there is still only one album with that name
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

    // Scenario: Changing only a song's album artist moves it to that artist's album of the same name
    //   Given two songs on the same album of one artist
    //   And one of them features a second artist
    //   When the user changes the album artist of the featuring song to the second artist
    //   Then that song is on an album of the same name by the second artist
    //   And the other song is still on the first artist's album
    //   And there are two albums with that name
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

    // Scenario: Using an existing album's name and album artist reuses that album
    //   Given a song without an album
    //   And another song on an existing artist's album
    //   When the user sets the first song's album, artist and album artist to the same as the other song's
    //   Then the first song is on that album, by that artist
    //   And the other song is unchanged
    //   And there is still only one album with that name
    //   And the artist exists
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

    // Scenario: Using a new album name with an existing artist creates a new album
    //   Given a song without an album
    //   And another song on an existing artist's album
    //   When the user sets the first song's album to a new name, with the existing artist as artist and album artist
    //   Then the first song is on the new album, by the existing artist
    //   And the new album exists
    //   And the existing album still exists
    //   And the existing artist exists
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

    // Scenario: Editing a song to match another song's title, album and artist gives its file a numbered name
    //   Given a song without an album
    //   And another song with a title, album and artist
    //   When the user sets the first song's title, album, artist and album artist to the same as the other song's
    //   Then the first song shows the same title, album and artist as the other song
    //   And its file path is the other song's path with " (2)" before the extension
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
