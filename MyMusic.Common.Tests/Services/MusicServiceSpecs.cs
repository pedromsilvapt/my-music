using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Models;
using MyMusic.Common.Services;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class MusicServiceSpecs
{
    [Fact]
    public async Task ImportMusic_EmptyDatabase_CreatesSongs()
    {
        // Arrange
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var job = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());

        MockMusicFile.Create(scenario.FileSystem, "/music/Title A.mp3", "Title A", "Album A", ["Artist A", "Artist B"],
            ["Genre A", "Genre B"]);
        MockMusicFile.Create(scenario.FileSystem, "/music/Title B.mp3", "Title B", "Album A", ["Artist A"],
            ["Genre A", "Genre B"]);
        MockMusicFile.Create(scenario.FileSystem, "/music/Title C.mp3", "Title C", "Album B", ["Artist C"],
            ["Genre B"]);

        // Act
        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music");

        // Assert
        job.SkipReasons.ShouldBeEmpty();

        // Songs
        var songs = LoadSongs(scenario.DbContext);
        songs.Count.ShouldBe(3);
        songs[0].ShouldSatisfyAllConditions(
            s => s.Title.ShouldBe("Title A"),
            s => s.Album.ShouldNotBeNull(),
            s => s.Album!.Name.ShouldBe("Album A"),
            s => s.Artists.Select(a => a.Artist.Name).ShouldBe(["Artist A", "Artist B"], ignoreOrder: true),
            s => s.Genres.Select(g => g.Genre.Name).ShouldBe(["Genre A", "Genre B"], ignoreOrder: true)
        );
        songs[1].ShouldSatisfyAllConditions(
            s => s.Title.ShouldBe("Title B"),
            s => s.Album.ShouldNotBeNull(),
            s => s.Album!.Name.ShouldBe("Album A"),
            s => s.Artists.Select(a => a.Artist.Name).ShouldBe(["Artist A"], ignoreOrder: true),
            s => s.Genres.Select(g => g.Genre.Name).ShouldBe(["Genre A", "Genre B"], ignoreOrder: true)
        );
        songs[2].ShouldSatisfyAllConditions(
            s => s.Title.ShouldBe("Title C"),
            s => s.Album.ShouldNotBeNull(),
            s => s.Album!.Name.ShouldBe("Album B"),
            s => s.Artists.Select(a => a.Artist.Name).ShouldBe(["Artist C"], ignoreOrder: true),
            s => s.Genres.Select(g => g.Genre.Name).ShouldBe(["Genre B"], ignoreOrder: true)
        );

        // Albums
        scenario.DbContext.Albums
            .Select(a => a.Name)
            .ToList()
            .ShouldBe(["Album A", "Album B"], ignoreOrder: true);

        // Artists
        scenario.DbContext.Artists
            .Select(a => a.Name)
            .ToList()
            .ShouldBe(["Artist A", "Artist B", "Artist C"], ignoreOrder: true);

        // Genres
        scenario.DbContext.Genres
            .Select(a => a.Name)
            .ToList()
            .ShouldBe(["Genre A", "Genre B"], ignoreOrder: true);
    }

    [Fact]
    public async Task ImportMusic_ExtractsAndStoresBitrate()
    {
        // Arrange
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var job = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());

        MockMusicFile.Create(scenario.FileSystem, "/music/Song.mp3", "Song", "Album", ["Artist"], ["Genre"]);

        // Act
        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music");

        // Assert
        var songs = LoadSongs(scenario.DbContext);
        songs.Count.ShouldBe(1);
        songs[0].Bitrate.ShouldNotBeNull();
        songs[0].Bitrate!.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ImportMusic_SamePathDifferentChecksum_ResolvesWithCounter()
    {
        // Two songs with identical metadata (same title/album/artist) but different file content
        // should both be stored, with the second getting a "(2)" suffix on its path
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var job = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());

        MockMusicFile.Create(scenario.FileSystem, "/music/Song.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);
        MockMusicFile.CreateWithDifferentContent(scenario.FileSystem, "/music/Song_v2.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);

        // Act
        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music");

        // Assert
        job.SkipReasons.ShouldBeEmpty();
        var songs = LoadSongs(scenario.DbContext);
        songs.Count.ShouldBe(2);

        var paths = songs.Select(s => s.RepositoryPath).ToList();
        paths.ShouldContain("/data/admin/My Artist/My Album/Song - My Artist.mp3");
        paths.ShouldContain("/data/admin/My Artist/My Album/Song - My Artist (2).mp3");
    }

    [Fact]
    public async Task ImportMusic_SamePathDifferentChecksum_ThreeSongs_ResolvesWithIncrementingCounters()
    {
        // Three songs with identical metadata but different content should get base, (2), and (3) paths
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var job = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());

        MockMusicFile.Create(scenario.FileSystem, "/music/Song.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);
        MockMusicFile.CreateWithDifferentContent(scenario.FileSystem, "/music/Song_v2.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);
        MockMusicFile.CreateWithDifferentContent(scenario.FileSystem, "/music/Song_v3.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);

        // Act
        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music");

        // Assert
        job.SkipReasons.ShouldBeEmpty();
        var songs = LoadSongs(scenario.DbContext);
        songs.Count.ShouldBe(3);

        var paths = songs.Select(s => s.RepositoryPath).ToList();
        paths.ShouldContain("/data/admin/My Artist/My Album/Song - My Artist.mp3");
        paths.ShouldContain("/data/admin/My Artist/My Album/Song - My Artist (2).mp3");
        paths.ShouldContain("/data/admin/My Artist/My Album/Song - My Artist (3).mp3");
    }

    [Fact]
    public async Task ImportMusic_SameChecksum_SamePath_DoesNotCreateDuplicateSongs()
    {
        // Importing the same file twice should not create a duplicate song.
        // The second import either skips (if file exists on disk) or updates the existing song.
        // Either way, only one song should exist in the DB.
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();

        MockMusicFile.Create(scenario.FileSystem, "/music/Song.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);

        // Act - import once
        var job1 = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());
        await musicService.ImportRepositorySongs(scenario.DbContext, job1, scenario.AdminUser.Id, "/music",
            duplicatesStrategy: DuplicateSongsHandlingStrategy.Skip);
        job1.Exceptions.ShouldBeEmpty();

        var songsAfterFirst = LoadSongs(scenario.DbContext);
        songsAfterFirst.Count.ShouldBe(1);

        // Act - import the same directory again (same file, same checksum)
        var job2 = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());
        await musicService.ImportRepositorySongs(scenario.DbContext, job2, scenario.AdminUser.Id, "/music",
            duplicatesStrategy: DuplicateSongsHandlingStrategy.Skip);
        job2.Exceptions.ShouldBeEmpty();

        // Assert - still only one song (not duplicated, path collision resolved or updated)
        var songsAfterSecond = LoadSongs(scenario.DbContext);
        songsAfterSecond.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ImportMusic_EmptyDatabase_IncrementsArtistSongsCount()
    {
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var job = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());

        MockMusicFile.Create(scenario.FileSystem, "/music/Title A.mp3", "Title A", "Album A", ["Artist A", "Artist B"],
            ["Genre A", "Genre B"]);
        MockMusicFile.Create(scenario.FileSystem, "/music/Title B.mp3", "Title B", "Album A", ["Artist A"],
            ["Genre A", "Genre B"]);

        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music");
        job.Exceptions.ShouldBeEmpty();

        var artists = scenario.DbContext.Artists.AsNoTracking().ToList();
        var artistA = artists.First(a => a.Name == "Artist A");
        var artistB = artists.First(a => a.Name == "Artist B");

        artistA.SongsCount.ShouldBe(2);
        artistB.SongsCount.ShouldBe(1);
    }

    [Fact]
    public async Task ImportMusic_ReImportSameSongs_DoesNotDoubleIncrementSongsCount()
    {
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();

        MockMusicFile.Create(scenario.FileSystem, "/music/Song.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);

        var job1 = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());
        await musicService.ImportRepositorySongs(scenario.DbContext, job1, scenario.AdminUser.Id, "/music",
            duplicatesStrategy: DuplicateSongsHandlingStrategy.Skip);
        job1.Exceptions.ShouldBeEmpty();

        var artistAfterFirst = scenario.DbContext.Artists.First(a => a.Name == "My Artist");
        var songsCountAfterFirst = artistAfterFirst.SongsCount;

        var job2 = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());
        await musicService.ImportRepositorySongs(scenario.DbContext, job2, scenario.AdminUser.Id, "/music",
            duplicatesStrategy: DuplicateSongsHandlingStrategy.Skip);
        job2.Exceptions.ShouldBeEmpty();

        var artistAfterSecond = scenario.DbContext.Artists.First(a => a.Name == "My Artist");
        artistAfterSecond.SongsCount.ShouldBe(songsCountAfterFirst);
    }

    [Fact]
    public async Task ImportMusic_ReImportSameChecksum_BumpsModifiedAtButNotFileModifiedAt()
    {
        // Re-importing an existing song whose file content has NOT changed (same checksum) must
        // bump ModifiedAt (any row-level field may have changed) but NOT FileModifiedAt, which
        // only changes on checksum change. This is the core "minimize updates during sync" behavior.
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();

        // First import: creates the song and seeds timestamps from the file's mtime.
        MockMusicFile.Create(scenario.FileSystem, "/music/Song.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);
        var job1 = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());
        await musicService.ImportRepositorySongs(scenario.DbContext, job1, scenario.AdminUser.Id, "/music",
            duplicatesStrategy: DuplicateSongsHandlingStrategy.Skip);
        job1.Exceptions.ShouldBeEmpty();

        var song = LoadSongs(scenario.DbContext).Single();
        var originalFileModifiedAt = song.FileModifiedAt;
        originalFileModifiedAt.ShouldNotBeNull();

        // Second import: re-import the SAME file (same checksum) via the metadata overload with
        // an explicit SongId pointing at the existing song. The re-import branch must detect the
        // unchanged checksum and leave FileModifiedAt alone, while still bumping ModifiedAt.
        var sourcePath = "/music/Song.mp3";
        var reimportModifiedAt = originalFileModifiedAt!.Value.AddHours(1);
        var metadata = new SongImportMetadata(sourcePath, DateTime.UtcNow, reimportModifiedAt, song.Id);
        var job2 = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());
        await musicService.ImportRepositorySongs(scenario.DbContext, job2, scenario.AdminUser.Id, [metadata],
            duplicatesStrategy: DuplicateSongsHandlingStrategy.Skip);
        job2.Exceptions.ShouldBeEmpty();

        var updatedSong = LoadSongs(scenario.DbContext).Single();
        updatedSong.FileModifiedAt.ShouldBe(originalFileModifiedAt, "FileModifiedAt must NOT change when checksum is unchanged");
        updatedSong.ModifiedAt.ShouldBe(reimportModifiedAt, "ModifiedAt must bump on every re-import (row-level change), regardless of checksum");
    }

    [Fact]
    public async Task ImportMusic_ReImportDifferentChecksum_BumpsFileModifiedAtAndModifiedAt()
    {
        // Re-importing an existing song whose file content HAS changed (different checksum) must
        // bump both FileModifiedAt and ModifiedAt to the new file mtime.
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();

        // First import: creates the song with the original content.
        MockMusicFile.Create(scenario.FileSystem, "/music/Song.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);
        var job1 = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());
        await musicService.ImportRepositorySongs(scenario.DbContext, job1, scenario.AdminUser.Id, "/music",
            duplicatesStrategy: DuplicateSongsHandlingStrategy.Skip);
        job1.Exceptions.ShouldBeEmpty();

        var song = LoadSongs(scenario.DbContext).Single();
        var originalChecksum = song.Checksum;
        var originalFileModifiedAt = song.FileModifiedAt;
        originalFileModifiedAt.ShouldNotBeNull();

        // Replace the file on disk with different content (different checksum) and re-import via
        // the metadata overload pointing at the same SongId.
        MockMusicFile.CreateWithDifferentContent(scenario.FileSystem, "/music/Song.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);
        var reimportModifiedAt = originalFileModifiedAt!.Value.AddHours(2);
        var metadata = new SongImportMetadata("/music/Song.mp3", DateTime.UtcNow, reimportModifiedAt, song.Id);
        var job2 = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());
        await musicService.ImportRepositorySongs(scenario.DbContext, job2, scenario.AdminUser.Id, [metadata],
            duplicatesStrategy: DuplicateSongsHandlingStrategy.Skip);
        job2.Exceptions.ShouldBeEmpty();
        job2.SkipReasons.ShouldBeEmpty($"Re-import should not skip. SkipReasons: {string.Join(", ", job2.SkipReasons.Select(r => r.Message))}");

        // Re-importing with a different checksum should update the existing song, not create a second one.
        var songsAfterReimport = LoadSongs(scenario.DbContext);
        songsAfterReimport.Count.ShouldBe(1, "Re-import with different checksum should update the existing song, not create a new one");

        var updatedSong = songsAfterReimport.Single();
        updatedSong.FileModifiedAt.ShouldBe(reimportModifiedAt, "FileModifiedAt must be bumped to the new file mtime when checksum changes");
        updatedSong.ModifiedAt.ShouldBe(reimportModifiedAt, "ModifiedAt must be bumped to the new file mtime when checksum changes");
        updatedSong.Checksum.ShouldNotBe(originalChecksum);
    }

    private static List<Song> LoadSongs(MusicDbContext context)
    {
        return context.Songs
            .OrderBy(s => s.Title)
            .Include(s => s.Artists)
            .Include(s => s.Genres)
            .Include(s => s.Cover)
            .Include(s => s.Album)
            .ThenInclude(a => a!.Artist)
            .AsSplitQuery()
            .ToList();
    }
}