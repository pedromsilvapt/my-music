using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;
using MyMusic.Common.Services.Sync;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Devices;

public class DeviceNamingPreviewServiceSpecs
{
    private const string DefaultTemplate = "Default/{{ title }}{{ extension }}";
    private const string TitleTemplate = "{{ title }}{{ extension }}";

    private static DeviceNamingPreviewService CreateService(Scenario scenario) =>
        new(
            scenario.DbContext,
            new DeviceLookupService(),
            new SyncPathResolver(),
            Options.Create(new Config
            {
                MusicRepositoryPath = "/music",
                DefaultNamingTemplate = DefaultTemplate,
            }));

    private static Task<DeviceNamingPreviewResult?> Preview(
        Scenario scenario, Device? device, string? namingTemplate, Song? song = null) =>
        CreateService(scenario).PreviewAsync(
            scenario.AdminUser.Id, device?.Id, song?.Id, namingTemplate, CancellationToken.None);

    [Fact]
    public async Task Preview_PathAlreadyMatchesTemplate_IsUnchanged()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        scenario.CreateSongDevice(device, scenario.CreateSong("One"), "One.mp3");

        // Act
        var result = await Preview(scenario, device, TitleTemplate);

        // Assert
        result.ShouldNotBeNull();
        result.Errors.ShouldBeEmpty();
        result.Total.ShouldBe(1);
        result.Renamed.ShouldBe(0);
        result.Songs[0].CurrentPath.ShouldBe("One.mp3");
        result.Songs[0].NewPath.ShouldBe("One.mp3");
        result.Songs[0].Changed.ShouldBeFalse();
        result.Songs[0].Error.ShouldBeNull();
    }

    [Fact]
    public async Task Preview_PathDiffersFromTemplate_IsRenamed()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("One");
        var songDevice = scenario.CreateSongDevice(device, song, "Old/file.flac");
        scenario.CreateSongDevice(device, scenario.CreateSong("Two"), "Two.mp3");

        // Act
        var result = await Preview(scenario, device, TitleTemplate);

        // Assert
        result.ShouldNotBeNull();
        result.Total.ShouldBe(2);
        result.Renamed.ShouldBe(1);

        // Ordered by current path
        result.Songs[0].SongDeviceId.ShouldBe(songDevice.Id);
        result.Songs[0].SongId.ShouldBe(song.Id);
        result.Songs[0].Title.ShouldBe("One");
        result.Songs[0].CurrentPath.ShouldBe("Old/file.flac");
        result.Songs[0].NewPath.ShouldBe("One.flac");
        result.Songs[0].Changed.ShouldBeTrue();
        result.Songs[1].Changed.ShouldBeFalse();
    }

    [Fact]
    public async Task Preview_TwoSongsGetTheSamePath_SecondGetsCollisionSuffix()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        scenario.CreateSongDevice(device, scenario.CreateSong("Same", repositoryPath: "/music/1.mp3"), "a.mp3");
        scenario.CreateSongDevice(device, scenario.CreateSong("Same", repositoryPath: "/music/2.mp3"), "b.mp3");

        // Act
        var result = await Preview(scenario, device, TitleTemplate);

        // Assert
        result.ShouldNotBeNull();
        result.Renamed.ShouldBe(2);
        result.Songs[0].NewPath.ShouldBe("Same.mp3");
        result.Songs[1].NewPath.ShouldBe("Same (2).mp3");
    }

    [Fact]
    public async Task Preview_PathTakenByAnotherFile_GetsCollisionSuffix()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        scenario.CreateSongDevice(device, scenario.CreateSong("One"), "Old/One.mp3");
        // A file that is not a song of the server (yet) holds the path the template gives
        scenario.CreateSongDevice(device, null, "One.mp3");

        // Act
        var result = await Preview(scenario, device, TitleTemplate);

        // Assert
        result.ShouldNotBeNull();
        result.Total.ShouldBe(1);
        result.Songs[0].NewPath.ShouldBe("One (2).mp3");
    }

    [Fact]
    public async Task Preview_RequestedPath_IsUsedInsteadOfTemplate()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var songDevice = scenario.CreateSongDevice(device, scenario.CreateSong("One"), "One.mp3");
        songDevice.RequestedPath = "Typed/Path.mp3";
        scenario.DbContext.SaveChanges();

        // Act
        var result = await Preview(scenario, device, TitleTemplate);

        // Assert
        result.ShouldNotBeNull();
        result.Songs[0].NewPath.ShouldBe("Typed/Path.mp3");
        result.Songs[0].Changed.ShouldBeTrue();
    }

    [Fact]
    public async Task Preview_CopiesMarkedForRemoval_AreSkippedAndFreeTheirPath()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        scenario.CreateSongDevice(device, scenario.CreateSong("One"), "Old/One.mp3");
        scenario.CreateSongDevice(device, scenario.CreateSong("Gone"), "One.mp3", syncAction: SongSyncAction.Remove);

        // Act
        var result = await Preview(scenario, device, TitleTemplate);

        // Assert
        result.ShouldNotBeNull();
        result.Total.ShouldBe(1);
        result.Songs[0].CurrentPath.ShouldBe("Old/One.mp3");
        result.Songs[0].NewPath.ShouldBe("One.mp3");
    }

    [Fact]
    public async Task Preview_TemplateWithSyntaxError_ReturnsErrorsAndNoSongs()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        scenario.CreateSongDevice(device, scenario.CreateSong("One"), "One.mp3");

        // Act
        var result = await Preview(scenario, device, "{{ title }}\n{{ if year }}{{ extension }}");

        // Assert
        result.ShouldNotBeNull();
        result.Errors.ShouldNotBeEmpty();
        result.Errors[0].Message.ShouldNotBeNullOrWhiteSpace();
        result.Errors[0].Line.ShouldBeGreaterThanOrEqualTo(1);
        result.Errors[0].Column.ShouldBeGreaterThanOrEqualTo(1);
        result.Total.ShouldBe(0);
        result.Songs.ShouldBeEmpty();
    }

    [Fact]
    public async Task Preview_TemplateFailingForASong_ReturnsErrorOnThatSong()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        scenario.CreateSongDevice(device, scenario.CreateSong("One"), "One.mp3");

        // Act
        var result = await Preview(scenario, device, "{{ title | math.plus 1 | string.missing }}");

        // Assert
        result.ShouldNotBeNull();
        result.Errors.ShouldBeEmpty();
        result.Total.ShouldBe(1);
        result.Renamed.ShouldBe(0);
        result.Songs[0].Error.ShouldNotBeNullOrWhiteSpace();
        result.Songs[0].NewPath.ShouldBe("One.mp3");
        result.Songs[0].Changed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Preview_BlankTemplate_UsesDefaultTemplate(string? namingTemplate)
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        scenario.CreateSongDevice(device, scenario.CreateSong("One"), "One.mp3");

        // Act
        var result = await Preview(scenario, device, namingTemplate);

        // Assert
        result.ShouldNotBeNull();
        result.DefaultNamingTemplate.ShouldBe(DefaultTemplate);
        result.Songs[0].NewPath.ShouldBe("Default/One.mp3");
    }

    [Fact]
    public async Task Preview_WithoutDevice_OnlyValidatesTemplate()
    {
        // Arrange
        var scenario = new Scenario();

        // Act
        var valid = await Preview(scenario, null, TitleTemplate);
        var invalid = await Preview(scenario, null, "{{ title | }}");

        // Assert
        valid.ShouldNotBeNull();
        valid.Errors.ShouldBeEmpty();
        valid.Songs.ShouldBeEmpty();
        valid.DefaultNamingTemplate.ShouldBe(DefaultTemplate);
        invalid.ShouldNotBeNull();
        invalid.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Preview_OtherUsersDevice_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var device = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);

        // Act
        var result = await Preview(scenario, device, TitleTemplate);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Preview_Song_GetsTemplatePathWithItsFileExtension()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("One", repositoryPath: "/music/Stored/file.flac");

        // Act
        var result = await Preview(scenario, null, "Songs/{{ title }}{{ extension }}", song);

        // Assert
        result.ShouldNotBeNull();
        result.Errors.ShouldBeEmpty();
        result.Songs.ShouldBeEmpty();
        result.Song.ShouldNotBeNull();
        result.Song.Path.ShouldBe("Songs/One.flac");
        result.Song.Error.ShouldBeNull();
    }

    [Fact]
    public async Task Preview_Song_HasNoOriginalFolderOrName()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("One", repositoryPath: "/music/Stored/file.mp3");

        // Act
        var result = await Preview(scenario, null, "[{{ original_folder }}][{{ original_name }}]{{ title }}", song);

        // Assert
        result.ShouldNotBeNull();
        result.Song.ShouldNotBeNull();
        result.Song.Path.ShouldBe("[][]One");
    }

    [Fact]
    public async Task Preview_SongWithBlankTemplate_UsesDefaultTemplate()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("One", repositoryPath: "/music/One.mp3");

        // Act
        var result = await Preview(scenario, null, null, song);

        // Assert
        result.ShouldNotBeNull();
        result.Song.ShouldNotBeNull();
        result.Song.Path.ShouldBe("Default/One.mp3");
    }

    [Fact]
    public async Task Preview_SongWithTemplateSyntaxError_ReturnsErrorsAndNoPath()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("One");

        // Act
        var result = await Preview(scenario, null, "{{ if year }}{{ title }}", song);

        // Assert
        result.ShouldNotBeNull();
        result.Errors.ShouldNotBeEmpty();
        result.Song.ShouldBeNull();
    }

    [Fact]
    public async Task Preview_TemplateFailingForTheSong_ReturnsItsError()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("One");

        // Act
        var result = await Preview(scenario, null, "{{ title | math.plus 1 | string.missing }}", song);

        // Assert
        result.ShouldNotBeNull();
        result.Errors.ShouldBeEmpty();
        result.Song.ShouldNotBeNull();
        result.Song.Path.ShouldBeNull();
        result.Song.Error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Preview_OtherUsersSong_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var song = scenario.CreateSong("Theirs", ownerId: otherUser.Id);

        // Act
        var result = await Preview(scenario, null, TitleTemplate, song);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Preview_WithoutSong_HasNoSongPath()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        scenario.CreateSongDevice(device, scenario.CreateSong("One"), "One.mp3");

        // Act
        var result = await Preview(scenario, device, TitleTemplate);

        // Assert
        result.ShouldNotBeNull();
        result.Total.ShouldBe(1);
        result.Song.ShouldBeNull();
    }

    [Fact]
    public async Task Preview_DoesNotChangeTheDevice()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice(namingTemplate: TitleTemplate);
        var songDevice = scenario.CreateSongDevice(device, scenario.CreateSong("One"), "Old/One.mp3");

        // Act
        await Preview(scenario, device, "New/{{ title }}{{ extension }}");

        // Assert
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Devices.Single(d => d.Id == device.Id).NamingTemplate.ShouldBe(TitleTemplate);
        scenario.DbContext.SongDevices.Single(sd => sd.Id == songDevice.Id).DevicePath.ShouldBe("Old/One.mp3");
    }
}
