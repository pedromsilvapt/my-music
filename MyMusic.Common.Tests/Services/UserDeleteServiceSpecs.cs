using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.SongHistory.Models;
using NSubstitute;
using Shouldly;
using SongHistoryEntity = MyMusic.Common.Entities.SongHistory;

namespace MyMusic.Common.Tests.Services;

public class UserDeleteServiceSpecs
{
    private (UserDeleteService service, Scenario scenario, IFileSystem fileSystem) CreateService()
    {
        var scenario = new Scenario();
        var fileSystem = Scenario.CreateFileSystem();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data" });
        var logger = Substitute.For<ILogger<UserDeleteService>>();

        var service = new UserDeleteService(
            scenario.DbContext,
            fileSystem,
            config,
            logger);

        return (service, scenario, fileSystem);
    }

    private static SongHistoryQueue CreateQueueEntry(
        Scenario scenario,
        long songId,
        int revision,
        long? transactionId = null,
        int errorCount = 0,
        DateTime? processedAt = null)
    {
        var entry = new SongHistoryQueue
        {
            SongId = songId,
            SongRevision = revision,
            TransactionId = transactionId,
            Data = new SongSnapshot
            {
                Title = "Song",
                Label = "Song",
                RepositoryPath = "/music/song.mp3",
                Checksum = "abc",
                ChecksumAlgorithm = "XxHash128",
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                Action = "updated",
            },
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = processedAt,
            ErrorCount = errorCount,
            LastError = errorCount >= 3 ? "Something went wrong" : null,
        };
        scenario.DbContext.SongHistoryQueues.Add(entry);
        scenario.DbContext.SaveChanges();
        return entry;
    }

    private static SongHistoryEntity CreateHistoryEntry(Scenario scenario, long songId, int revision)
    {
        var entry = new SongHistoryEntity
        {
            SongId = songId,
            SongRevision = revision,
            Diff = new SongHistoryDelta { Action = "updated" },
            CreatedAt = DateTime.UtcNow,
        };
        scenario.DbContext.SongHistories.Add(entry);
        scenario.DbContext.SaveChanges();
        return entry;
    }

    [Fact]
    public async Task DeleteAsync_DeletesUser()
    {
        // Arrange
        var (service, scenario, _) = CreateService();
        var user = scenario.CreateUser("Test", "testuser");

        // Act
        var result = await service.DeleteAsync(user.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(user.Id);
        scenario.DbContext.Users.Any(u => u.Id == user.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNullWhenUserNotFound()
    {
        // Arrange
        var (service, scenario, _) = CreateService();

        // Act
        var result = await service.DeleteAsync(999999);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_DeletesSongs()
    {
        // Arrange
        var (service, scenario, _) = CreateService();
        var user = scenario.CreateUser("Test", "testuser");
        var artist = scenario.CreateArtist("Artist", user.Id);
        var album = scenario.CreateAlbum("Album", artist, user.Id);
        var song = scenario.CreateSong("Song", ownerId: user.Id, album: album);

        // Act
        await service.DeleteAsync(user.Id);

        // Assert
        scenario.DbContext.Songs.Any(s => s.Id == song.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DeletesPlaylists()
    {
        // Arrange
        var (service, scenario, _) = CreateService();
        var user = scenario.CreateUser("Test", "testuser");
        var playlist = scenario.CreatePlaylist("My Playlist", user.Id);

        // Act
        await service.DeleteAsync(user.Id);

        // Assert
        scenario.DbContext.Playlists.Any(p => p.Id == playlist.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DeletesPlayHistory()
    {
        // Arrange
        var (service, scenario, _) = CreateService();
        var user = scenario.CreateUser("Test", "testuser");
        var artist = scenario.CreateArtist("Artist", user.Id);
        var album = scenario.CreateAlbum("Album", artist, user.Id);
        var song = scenario.CreateSong("Song", ownerId: user.Id, album: album);
        scenario.DbContext.Add(new PlayHistory
        {
            SongId = song.Id,
            OwnerId = user.Id,
            ClientId = "test",
            PlayedAt = DateTime.UtcNow,
        });
        scenario.DbContext.SaveChanges();

        // Act
        await service.DeleteAsync(user.Id);

        // Assert
        scenario.DbContext.PlayHistories.Any(h => h.OwnerId == user.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DeletesSongHistory()
    {
        // Arrange
        var (service, scenario, _) = CreateService();
        var user = scenario.CreateUser("Test", "testuser");
        var artist = scenario.CreateArtist("Artist", user.Id);
        var album = scenario.CreateAlbum("Album", artist, user.Id);
        var song = scenario.CreateSong("Song", ownerId: user.Id, album: album);
        CreateHistoryEntry(scenario, song.Id, 1);
        CreateHistoryEntry(scenario, song.Id, 2);

        // Act
        await service.DeleteAsync(user.Id);

        // Assert
        scenario.DbContext.SongHistories.Any(h => h.SongId == song.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DeletesSongHistoryQueue()
    {
        // Arrange
        var (service, scenario, _) = CreateService();
        var user = scenario.CreateUser("Test", "testuser");
        var artist = scenario.CreateArtist("Artist", user.Id);
        var album = scenario.CreateAlbum("Album", artist, user.Id);
        var song = scenario.CreateSong("Song", ownerId: user.Id, album: album);
        CreateQueueEntry(scenario, song.Id, 1, transactionId: 100);
        CreateQueueEntry(scenario, song.Id, 2, transactionId: 100);

        // Act
        await service.DeleteAsync(user.Id);

        // Assert
        scenario.DbContext.SongHistoryQueues.Any(q => q.SongId == song.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DeletesDeadLetteredQueueEntries()
    {
        // Arrange — dead-lettered entries have error_count >= 3 and processed_at IS NULL.
        // In production, the PostgreSQL BEFORE DELETE trigger blocks song deletion when
        // such entries exist. The service must clean them up before deleting songs.
        var (service, scenario, _) = CreateService();
        var user = scenario.CreateUser("Test", "testuser");
        var artist = scenario.CreateArtist("Artist", user.Id);
        var album = scenario.CreateAlbum("Album", artist, user.Id);
        var song = scenario.CreateSong("Song", ownerId: user.Id, album: album);
        CreateQueueEntry(scenario, song.Id, 1, errorCount: 3, processedAt: null);

        // Act — should not throw, and should clean up the dead-lettered entry.
        var result = await service.DeleteAsync(user.Id);

        // Assert
        result.ShouldNotBeNull();
        scenario.DbContext.SongHistoryQueues.Any(q => q.SongId == song.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DeletesProcessedQueueEntries()
    {
        // Arrange — processed entries (processed_at != null) should also be cleaned up.
        var (service, scenario, _) = CreateService();
        var user = scenario.CreateUser("Test", "testuser");
        var artist = scenario.CreateArtist("Artist", user.Id);
        var album = scenario.CreateAlbum("Album", artist, user.Id);
        var song = scenario.CreateSong("Song", ownerId: user.Id, album: album);
        CreateQueueEntry(scenario, song.Id, 1, processedAt: DateTime.UtcNow);

        // Act
        await service.DeleteAsync(user.Id);

        // Assert
        scenario.DbContext.SongHistoryQueues.Any(q => q.SongId == song.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DeletesSongHistoryForMultipleSongs()
    {
        // Arrange
        var (service, scenario, _) = CreateService();
        var user = scenario.CreateUser("Test", "testuser");
        var artist = scenario.CreateArtist("Artist", user.Id);
        var album = scenario.CreateAlbum("Album", artist, user.Id);
        var song1 = scenario.CreateSong("Song 1", ownerId: user.Id, album: album);
        var song2 = scenario.CreateSong("Song 2", ownerId: user.Id, album: album);
        CreateHistoryEntry(scenario, song1.Id, 1);
        CreateHistoryEntry(scenario, song2.Id, 1);
        CreateQueueEntry(scenario, song1.Id, 1);
        CreateQueueEntry(scenario, song2.Id, 1);

        // Act
        await service.DeleteAsync(user.Id);

        // Assert
        scenario.DbContext.SongHistories.Any(h => h.SongId == song1.Id || h.SongId == song2.Id).ShouldBeFalse();
        scenario.DbContext.SongHistoryQueues.Any(q => q.SongId == song1.Id || q.SongId == song2.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DoesNotDeleteOtherUsersSongHistory()
    {
        // Arrange — another user's song history must remain untouched.
        var (service, scenario, _) = CreateService();
        var otherUser = scenario.CreateUser("Other", "otheruser");
        var otherArtist = scenario.CreateArtist("Artist", otherUser.Id);
        var otherAlbum = scenario.CreateAlbum("Album", otherArtist, otherUser.Id);
        var otherSong = scenario.CreateSong("Other Song", ownerId: otherUser.Id, album: otherAlbum);
        CreateHistoryEntry(scenario, otherSong.Id, 1);
        CreateQueueEntry(scenario, otherSong.Id, 1);

        var user = scenario.CreateUser("Test", "testuser");
        var artist = scenario.CreateArtist("Artist2", user.Id);
        var album = scenario.CreateAlbum("Album2", artist, user.Id);
        var song = scenario.CreateSong("Song", ownerId: user.Id, album: album);
        CreateHistoryEntry(scenario, song.Id, 1);
        CreateQueueEntry(scenario, song.Id, 1);

        // Act
        await service.DeleteAsync(user.Id);

        // Assert — other user's history and queue entries remain.
        scenario.DbContext.SongHistories.Any(h => h.SongId == otherSong.Id).ShouldBeTrue();
        scenario.DbContext.SongHistoryQueues.Any(q => q.SongId == otherSong.Id).ShouldBeTrue();
        // Deleted user's entries are gone.
        scenario.DbContext.SongHistories.Any(h => h.SongId == song.Id).ShouldBeFalse();
        scenario.DbContext.SongHistoryQueues.Any(q => q.SongId == song.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DeletesUserMusicDirectory()
    {
        // Arrange
        var (service, scenario, fileSystem) = CreateService();
        var user = scenario.CreateUser("Test", "testuser");
        var userMusicDir = fileSystem.Path.Combine("/data", "testuser");
        fileSystem.Directory.CreateDirectory(userMusicDir);

        // Act
        await service.DeleteAsync(user.Id);

        // Assert
        fileSystem.Directory.Exists(userMusicDir).ShouldBeFalse();
    }
}
