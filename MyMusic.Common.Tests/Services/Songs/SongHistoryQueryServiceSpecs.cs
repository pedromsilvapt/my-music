using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.SongHistory.Models;
using MyMusic.Common.Services.Songs;
using NSubstitute;
using Shouldly;
using SongHistoryEntity = MyMusic.Common.Entities.SongHistory;

namespace MyMusic.Common.Tests.Services.Songs;

public class SongHistoryQueryServiceSpecs
{
    private static (SongHistoryQueryService service, ICurrentUser currentUser) CreateService(
        Scenario scenario,
        long? userId = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(userId ?? scenario.AdminUser.Id);
        var service = new SongHistoryQueryService(scenario.DbContext, currentUser);
        return (service, currentUser);
    }

    private static SongHistoryEntity CreateHistoryRow(long songId, int revision, string? action = null)
    {
        return new SongHistoryEntity
        {
            SongId = songId,
            SongRevision = revision,
            Diff = new SongHistoryDelta { Action = action ?? (revision == 1 ? "created" : "updated") },
            CreatedAt = DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task GetSongHistoryAsync_SongExists_ReturnsHistoryInRevisionOrder()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("History Song");
        // Insert rows out of order (revisions 3, 1, 2) to verify ascending ordering.
        scenario.DbContext.SongHistories.AddRange(
            CreateHistoryRow(song.Id, 3),
            CreateHistoryRow(song.Id, 1),
            CreateHistoryRow(song.Id, 2));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetSongHistoryAsync(song.Id, CancellationToken.None);

        // Assert
        result.Count.ShouldBe(3);
        result.Select(h => h.SongRevision).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task GetSongHistoryAsync_NoHistory_ReturnsEmptyList()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("No History Song");
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetSongHistoryAsync(song.Id, CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSongHistoryAsync_SongBelongsToAnotherUser_ReturnsEmpty()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherSong = scenario.CreateSong("Other's Song", ownerId: otherUser.Id);
        scenario.DbContext.SongHistories.AddRange(
            CreateHistoryRow(otherSong.Id, 1),
            CreateHistoryRow(otherSong.Id, 2));
        await scenario.DbContext.SaveChangesAsync();
        var (service, _) = CreateService(scenario, userId: scenario.AdminUser.Id);

        // Act
        var result = await service.GetSongHistoryAsync(otherSong.Id, CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSongHistoryAsync_SongDeleted_ReturnsEmpty()
    {
        // Arrange - seed a song with history, then delete the song row (history persists).
        var scenario = new Scenario();
        var song = scenario.CreateSong("Doomed Song");
        scenario.DbContext.SongHistories.AddRange(
            CreateHistoryRow(song.Id, 1),
            CreateHistoryRow(song.Id, 2));
        await scenario.DbContext.SaveChangesAsync();
        var songId = song.Id;

        scenario.DbContext.Songs.Remove(song);
        await scenario.DbContext.SaveChangesAsync();

        // History rows must still be present in the table.
        scenario.DbContext.SongHistories.Count(h => h.SongId == songId).ShouldBe(2);

        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetSongHistoryAsync(songId, CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSongHistoryAsync_UnknownSongId_ReturnsEmpty()
    {
        // Arrange
        var scenario = new Scenario();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.GetSongHistoryAsync(99999, CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }
}