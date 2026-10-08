using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Songs;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Songs;

public class SongTimestampsUpdateServiceSpecs
{
    private static readonly DateTime OldDate = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime CreatedAt = new(2015, 3, 4, 5, 6, 7, DateTimeKind.Utc);
    private static readonly DateTime ModifiedAt = new(2016, 4, 5, 6, 7, 8, DateTimeKind.Utc);
    private static readonly DateTime AddedAt = new(2017, 5, 6, 7, 8, 9, DateTimeKind.Utc);
    private static readonly DateTime FileModifiedAt = new(2018, 6, 7, 8, 9, 10, DateTimeKind.Utc);

    private static readonly SongTimestampsUpdate Timestamps = new()
    {
        CreatedAt = CreatedAt,
        ModifiedAt = ModifiedAt,
        AddedAt = AddedAt,
        FileModifiedAt = FileModifiedAt,
    };

    private readonly Scenario _scenario = new();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    public SongTimestampsUpdateServiceSpecs()
    {
        _currentUser.Id.Returns(_scenario.AdminUser.Id);
    }

    private SongTimestampsUpdateService CreateService() =>
        new(_scenario.DbContext, _currentUser, _scenario.AdvisoryLocks,
            Substitute.For<ILogger<SongTimestampsUpdateService>>());

    private Song ReloadSong(long songId) =>
        _scenario.DbContext.Songs.AsNoTracking().First(s => s.Id == songId);

    [Fact]
    public async Task UpdateAsync_StoresTimestampsExactlyAsGiven()
    {
        // Arrange
        var song = _scenario.CreateSong("Song", modifiedAt: OldDate, fileModifiedAt: OldDate);

        // Act
        var result = await CreateService().UpdateAsync(song.Id, Timestamps);

        // Assert
        var reloaded = ReloadSong(song.Id);
        reloaded.ShouldSatisfyAllConditions(
            () => reloaded.CreatedAt.ShouldBe(CreatedAt),
            // ModifiedAt is the given one, not the time of the update
            () => reloaded.ModifiedAt.ShouldBe(ModifiedAt),
            () => reloaded.AddedAt.ShouldBe(AddedAt),
            () => reloaded.FileModifiedAt.ShouldBe(FileModifiedAt),
            () => result.Id.ShouldBe(song.Id),
            () => result.CreatedAt.ShouldBe(CreatedAt)
        );
    }

    [Fact]
    public async Task UpdateAsync_NullOptionalTimestamps_ClearsThem()
    {
        // Arrange
        var song = _scenario.CreateSong("Song", fileModifiedAt: OldDate);

        // Act
        await CreateService().UpdateAsync(song.Id, Timestamps with { AddedAt = null, FileModifiedAt = null });

        // Assert
        var reloaded = ReloadSong(song.Id);
        reloaded.ShouldSatisfyAllConditions(
            () => reloaded.AddedAt.ShouldBeNull(),
            () => reloaded.FileModifiedAt.ShouldBeNull(),
            () => reloaded.CreatedAt.ShouldBe(CreatedAt),
            () => reloaded.ModifiedAt.ShouldBe(ModifiedAt)
        );
    }

    [Fact]
    public async Task UpdateAsync_LocalTimestamps_AreStoredAsUtc()
    {
        // Arrange
        var song = _scenario.CreateSong("Song");
        var localCreatedAt = new DateTime(2015, 3, 4, 5, 6, 7, DateTimeKind.Local);

        // Act
        await CreateService().UpdateAsync(song.Id, Timestamps with { CreatedAt = localCreatedAt });

        // Assert
        ReloadSong(song.Id).CreatedAt.ShouldBe(localCreatedAt.ToUniversalTime());
    }

    [Fact]
    public async Task UpdateAsync_DoesNotChangeChecksumOrSongDevices()
    {
        // Arrange
        var song = _scenario.CreateSong("Song", checksum: "checksum");
        var device = _scenario.CreateDevice("Phone");
        var songDevice = _scenario.CreateSongDevice(device, song, "/music/song.mp3");

        // Act
        await CreateService().UpdateAsync(song.Id, Timestamps);

        // Assert
        ReloadSong(song.Id).Checksum.ShouldBe("checksum");
        var reloadedDevice = _scenario.DbContext.SongDevices.AsNoTracking().First(sd => sd.Id == songDevice.Id);
        reloadedDevice.SyncAction.ShouldBeNull();
        reloadedDevice.SyncActionReason.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_SongOfAnotherUser_Throws()
    {
        // Arrange
        var otherUser = _scenario.CreateUser("Other", "other");
        var song = _scenario.CreateSong("Song", ownerId: otherUser.Id, modifiedAt: OldDate);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => CreateService().UpdateAsync(song.Id, Timestamps));
        ReloadSong(song.Id).ModifiedAt.ShouldBe(OldDate);
    }

    [Fact]
    public async Task UpdateAsync_UnknownSong_Throws()
    {
        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => CreateService().UpdateAsync(123456, Timestamps));
    }
}
