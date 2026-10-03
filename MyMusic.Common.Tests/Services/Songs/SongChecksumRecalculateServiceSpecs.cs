using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Songs;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Songs;

public class SongChecksumRecalculateServiceSpecs
{
    private const string FilePath = "/data/song.mp3";
    private const string OutdatedChecksum = "outdated-checksum";

    private static readonly DateTime OldDate = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Scenario _scenario = new();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    public SongChecksumRecalculateServiceSpecs()
    {
        _currentUser.Id.Returns(_scenario.AdminUser.Id);
    }

    private SongChecksumRecalculateService CreateService() =>
        new(_scenario.DbContext, _currentUser, _scenario.FileSystem, _scenario.AdvisoryLocks,
            Substitute.For<ILogger<SongChecksumRecalculateService>>());

    /// <summary>
    /// Writes the song's file and returns its actual checksum.
    /// </summary>
    private string WriteFile(byte[]? content = null)
    {
        var fileSystem = _scenario.FileSystem;
        fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(FilePath)!);
        fileSystem.File.WriteAllBytes(FilePath, content ?? [1, 2, 3, 4]);

        return ChecksumService.CalculateChecksum(fileSystem, ChecksumService.CreateChecksumAlgorithm(), FilePath);
    }

    private Song ReloadSong(long songId) =>
        _scenario.DbContext.Songs.AsNoTracking().First(s => s.Id == songId);

    [Fact]
    public async Task RecalculateAsync_FileMatchesChecksum_ReturnsUnchangedAndDoesNotModifySong()
    {
        // Arrange
        var checksum = WriteFile();
        var song = _scenario.CreateSong("Song", repositoryPath: FilePath, checksum: checksum, modifiedAt: OldDate);

        // Act
        var result = await CreateService().RecalculateAsync(song.Id);

        // Assert
        result.Changed.ShouldBeFalse();
        var reloaded = ReloadSong(song.Id);
        reloaded.Checksum.ShouldBe(checksum);
        reloaded.ModifiedAt.ShouldBe(OldDate);
    }

    [Fact]
    public async Task RecalculateAsync_FileDiffers_UpdatesChecksumAndModifiedAt()
    {
        // Arrange
        var checksum = WriteFile();
        var song = _scenario.CreateSong("Song", repositoryPath: FilePath, checksum: OutdatedChecksum,
            checksumAlgorithm: "XxHash128", modifiedAt: OldDate);

        // Act
        var result = await CreateService().RecalculateAsync(song.Id);

        // Assert
        result.Changed.ShouldBeTrue();
        var reloaded = ReloadSong(song.Id);
        reloaded.Checksum.ShouldBe(checksum);
        reloaded.ChecksumAlgorithm.ShouldBe("XxHash128");
        reloaded.ModifiedAt.ShouldBeGreaterThan(OldDate);
    }

    [Fact]
    public async Task RecalculateAsync_FileDiffers_DoesNotChangeFileModifiedAt()
    {
        // Arrange
        WriteFile();
        var song = _scenario.CreateSong("Song", repositoryPath: FilePath, checksum: OutdatedChecksum,
            fileModifiedAt: OldDate);

        // Act
        await CreateService().RecalculateAsync(song.Id);

        // Assert
        ReloadSong(song.Id).FileModifiedAt.ShouldBe(OldDate);
    }

    [Fact]
    public async Task RecalculateAsync_FileDiffers_DoesNotChangeSongDevices()
    {
        // Arrange
        WriteFile();
        var song = _scenario.CreateSong("Song", repositoryPath: FilePath, checksum: OutdatedChecksum);
        var device = _scenario.CreateDevice("Phone");
        var songDevice = _scenario.CreateSongDevice(device, song, "/music/song.mp3");

        // Act
        await CreateService().RecalculateAsync(song.Id);

        // Assert
        var reloaded = _scenario.DbContext.SongDevices.AsNoTracking().First(sd => sd.Id == songDevice.Id);
        reloaded.SyncAction.ShouldBeNull();
        reloaded.SyncActionReason.ShouldBeNull();
    }

    [Fact]
    public async Task RecalculateAsync_FileMissing_Throws()
    {
        // Arrange
        var song = _scenario.CreateSong("Song", repositoryPath: FilePath, checksum: OutdatedChecksum);

        // Act & Assert
        await Should.ThrowAsync<FileNotFoundException>(() => CreateService().RecalculateAsync(song.Id));
        ReloadSong(song.Id).Checksum.ShouldBe(OutdatedChecksum);
    }

    [Fact]
    public async Task RecalculateAsync_SongOfAnotherUser_Throws()
    {
        // Arrange
        WriteFile();
        var otherUser = _scenario.CreateUser("Other", "other");
        var song = _scenario.CreateSong("Song", ownerId: otherUser.Id, repositoryPath: FilePath,
            checksum: OutdatedChecksum);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => CreateService().RecalculateAsync(song.Id));
        ReloadSong(song.Id).Checksum.ShouldBe(OutdatedChecksum);
    }
}
