using System.IO;
using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

/// <summary>
/// Specs for <see cref="SharedSongImportService"/> (Phase 7 — Import Shared Song).
///
/// These specs use a REAL on-disk <see cref="FileSystem"/> under a unique temp directory per
/// test, instead of <see cref="TestableIO.System.IO.Abstractions.MockFileSystem"/>. The reason:
/// <see cref="MusicService.ImportRepositorySongs"/> checks duplicate existence with the static
/// <c>System.IO.File.Exists</c> against the recipient's repository path (see MusicService.cs:387),
/// which is only populated when the recipient's copy actually lives on the real disk. The Skip
/// branch (and thus the "AlreadyImported" signal) only fires under those conditions, so a real FS
/// is required to faithfully exercise the idempotent re-import path.
/// </summary>
public class SharedSongImportServiceSpecs
{
    /// <summary>
    /// Creates a real on-disk scenario rooted under a fresh temp dir. Disposing it cleans the
    /// temp dir up. The <see cref="MusicDbContext"/> is the in-memory SQLite instance from
    /// <see cref="Scenario"/>, but the <see cref="MusicService"/> uses a real <see cref="FileSystem"/>.
    /// </summary>
    private sealed class RealFsScenario : IDisposable
    {
        public string Root { get; }
        public string RepoPath { get; }
        public string SourceDir { get; }
        public IFileSystem FileSystem { get; }
        public MusicDbContext DbContext { get; }
        public User Owner { get; }
        public User Recipient { get; }
        public MusicService MusicService { get; }
        public SharedSongImportService ImportService { get; }

        public RealFsScenario(string ownerUsername = "owner", string recipientUsername = "recipient")
        {
            Root = Path.Combine(Path.GetTempPath(), $"mymusic_shared_specs_{Guid.NewGuid():N}");
            RepoPath = Path.Combine(Root, "repo");
            SourceDir = Path.Combine(Root, "source");
            Directory.CreateDirectory(RepoPath);
            Directory.CreateDirectory(SourceDir);

            FileSystem = new FileSystem();
            DbContext = Scenario.CreateDbContext();
            Owner = CreateUser(ownerUsername);
            Recipient = CreateUser(recipientUsername);

            MusicService = new MusicService(
                FileSystem,
                Options.Create(new Config { MusicRepositoryPath = RepoPath }),
                Substitute.For<ISongMergeService>(),
                Substitute.For<ILogger<MusicService>>());

            ImportService = new SharedSongImportService(
                MusicService,
                FileSystem,
                Substitute.For<ILogger<MusicImportJob>>(),
                Substitute.For<ILogger<SharedSongImportService>>());
        }

        public User CreateUser(string username)
        {
            var user = new User { Name = username, Username = username };
            DbContext.Add(user);
            DbContext.SaveChanges();
            return user;
        }

        /// <summary>
        /// Stages a tagged mp3 at <paramref name="sourceFilePath"/> on the real disk and imports
        /// it into <paramref name="ownerId"/>'s library, returning the resulting owned Song.
        /// This is the "seed the owner's library" helper used to set up the shared-song scenario.
        /// </summary>
        public Song ImportOwnedSongForUser(
            string sourceFilePath,
            string title,
            string album,
            string[] artists,
            string[] genres,
            long ownerId)
        {
            MockMusicFile.Create(FileSystem, sourceFilePath, title, album, artists, genres);
            var job = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());
            // The folder-based import overload enumerates the directory the mp3 lives in.
            MusicService.ImportRepositorySongs(DbContext, job, ownerId, Path.GetDirectoryName(sourceFilePath)!).GetAwaiter().GetResult();
            job.Exceptions.ShouldBeEmpty();
            return DbContext.Songs.Single(s => s.OwnerId == ownerId && s.Title == title);
        }

        public void Dispose()
        {
            DbContext.Dispose();
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    /// <summary>
    /// Helper: create a shared-song scenario — owner imports a song, then shares it with the recipient.
    /// Returns the shared (owner-owned) Song and the scenario.
    /// </summary>
    private static (RealFsScenario scenario, Song ownerSong) CreateSharedSongScenario(
        string title = "Shared Title",
        string album = "Shared Album",
        string[]? artists = null,
        string[]? genres = null)
    {
        artists ??= ["Shared Artist"];
        genres ??= ["Shared Genre"];
        var scenario = new RealFsScenario();

        var sourcePath = Path.Combine(scenario.SourceDir, $"{title}.mp3");
        var ownerSong = scenario.ImportOwnedSongForUser(
            sourcePath, title, album, artists, genres, scenario.Owner.Id);

        // Share with recipient
        scenario.DbContext.SongSharings.Add(new SongSharing
        {
            SongId = ownerSong.Id,
            UserId = scenario.Recipient.Id,
            CreatedAt = DateTime.UtcNow,
        });
        scenario.DbContext.SaveChanges();

        return (scenario, ownerSong);
    }

    [Fact]
    public async Task ImportAsync_WhenShared_CreatesNewSongOwnedByRecipient()
    {
        // Arrange — owner imports a song and shares it with the recipient
        var (scenario, ownerSong) = CreateSharedSongScenario();

        // Act — recipient imports the shared song into their own library
        var result = await scenario.ImportService.ImportAsync(
            scenario.DbContext, ownerSong.Id, scenario.Recipient.Id, CancellationToken.None);

        // Assert — a fresh Song is created for the recipient with a different Id, owned by them,
        // carrying the same audio checksum as the owner's copy.
        result.Success.ShouldBeTrue();
        result.SongId.ShouldNotBeNull();
        result.SongId.Value.ShouldNotBe(ownerSong.Id);
        result.AlreadyImported.ShouldBeFalse();

        var recipientSong = scenario.DbContext.Songs.Single(s => s.Id == result.SongId.Value);
        recipientSong.OwnerId.ShouldBe(scenario.Recipient.Id);
        recipientSong.Checksum.ShouldBe(ownerSong.Checksum);
        recipientSong.ChecksumAlgorithm.ShouldBe(ownerSong.ChecksumAlgorithm);
    }

    [Fact]
    public async Task ImportAsync_WhenAlreadyImported_Skip_ReturnsExistingSongId()
    {
        // Arrange — recipient imports the shared song once (fresh copy)
        var (scenario, ownerSong) = CreateSharedSongScenario();

        var first = await scenario.ImportService.ImportAsync(
            scenario.DbContext, ownerSong.Id, scenario.Recipient.Id, CancellationToken.None);
        first.Success.ShouldBeTrue();
        first.AlreadyImported.ShouldBeFalse();

        var recipientSongsBefore = scenario.DbContext.Songs.Count(s => s.OwnerId == scenario.Recipient.Id);
        recipientSongsBefore.ShouldBe(1);

        // Act — recipient imports the SAME shared song again
        var second = await scenario.ImportService.ImportAsync(
            scenario.DbContext, ownerSong.Id, scenario.Recipient.Id, CancellationToken.None);

        // Assert — Skip strategy keeps the existing recipient-owned song; idempotent.
        second.Success.ShouldBeTrue();
        second.AlreadyImported.ShouldBeTrue();
        second.SongId.ShouldBe(first.SongId);

        var recipientSongsAfter = scenario.DbContext.Songs.Count(s => s.OwnerId == scenario.Recipient.Id);
        recipientSongsAfter.ShouldBe(1, "Re-importing must not create a duplicate song");
    }

    [Fact]
    public async Task ImportAsync_WhenNotShared_Throws403()
    {
        // Arrange — owner imports a song but does NOT share it with the recipient
        var scenario = new RealFsScenario();
        var sourcePath = Path.Combine(scenario.SourceDir, "Unshared.mp3");
        var ownerSong = scenario.ImportOwnedSongForUser(
            sourcePath, "Unshared Title", "Unshared Album", ["Artist"], ["Genre"], scenario.Owner.Id);

        // Act & Assert — recipient cannot import a song that is not shared with them
        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            scenario.ImportService.ImportAsync(
                scenario.DbContext, ownerSong.Id, scenario.Recipient.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ImportAsync_WhenSongNotFound_Throws404()
    {
        // Arrange
        var scenario = new RealFsScenario();

        // Act & Assert — importing a non-existent song id throws InvalidOperationException (404)
        await Should.ThrowAsync<InvalidOperationException>(() =>
            scenario.ImportService.ImportAsync(
                scenario.DbContext, songId: 999999, scenario.Recipient.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ImportAsync_RecreatesAlbumArtistGenreOwnedByRecipient()
    {
        // Arrange — owner imports a song with distinct album/artist/genre and shares it
        var (scenario, ownerSong) = CreateSharedSongScenario(
            title: "Recreate Title",
            album: "Recreate Album",
            artists: ["Recreate Artist"],
            genres: ["Recreate Genre"]);

        // Act — recipient imports the shared song
        var result = await scenario.ImportService.ImportAsync(
            scenario.DbContext, ownerSong.Id, scenario.Recipient.Id, CancellationToken.None);
        result.Success.ShouldBeTrue();

        // Assert — the recipient gets NEW Album/Artist/Genre entities owned by them, NOT references
        // to the owner's entities. Per-owner dedup means recipient's library is self-contained.
        var recipientSong = scenario.DbContext.Songs
            .Include(s => s.Album)
            .Include(s => s.Artists).ThenInclude(sa => sa.Artist)
            .Include(s => s.Genres).ThenInclude(sg => sg.Genre)
            .Single(s => s.Id == result.SongId.Value);

        recipientSong.Album.OwnerId.ShouldBe(scenario.Recipient.Id);
        recipientSong.Album.Id.ShouldNotBe(ownerSong.AlbumId);

        var recipientArtist = recipientSong.Artists.Single().Artist;
        recipientArtist.OwnerId.ShouldBe(scenario.Recipient.Id);
        recipientArtist.Id.ShouldNotBe(ownerSong.Artists.First().ArtistId);

        var recipientGenre = recipientSong.Genres.Single().Genre;
        recipientGenre.OwnerId.ShouldBe(scenario.Recipient.Id);
        recipientGenre.Id.ShouldNotBe(ownerSong.Genres.First().GenreId);
    }

    [Fact]
    public async Task ImportAsync_DoesNotCreateSongDevice()
    {
        // Arrange
        var (scenario, ownerSong) = CreateSharedSongScenario();

        // Act
        var result = await scenario.ImportService.ImportAsync(
            scenario.DbContext, ownerSong.Id, scenario.Recipient.Id, CancellationToken.None);
        result.Success.ShouldBeTrue();

        // Assert — no SongDevice rows are created for the recipient, matching normal Upload behavior
        var recipientSongDevices = scenario.DbContext.SongDevices
            .Where(sd => sd.SongId == result.SongId.Value)
            .ToList();
        recipientSongDevices.ShouldBeEmpty();
    }

    [Fact]
    public async Task ImportAsync_DeletesTempDir_OnSuccess()
    {
        // Arrange
        var (scenario, ownerSong) = CreateSharedSongScenario();
        var tempBefore = Directory.EnumerateDirectories(Path.GetTempPath(), "mymusic_shared_import_*").ToList();

        // Act
        await scenario.ImportService.ImportAsync(
            scenario.DbContext, ownerSong.Id, scenario.Recipient.Id, CancellationToken.None);

        // Assert — the temp staging dir created during import is gone after success
        var tempAfter = Directory.EnumerateDirectories(Path.GetTempPath(), "mymusic_shared_import_*").ToList();
        var leftover = tempAfter.Except(tempBefore).ToList();
        leftover.ShouldBeEmpty("temp import directory must be cleaned up on success");
    }

    [Fact]
    public async Task ImportAsync_DeletesTempDir_OnFailure()
    {
        // Arrange — a share exists, but the owner's RepositoryPath file is missing on disk so the
        // underlying import fails (staging OpenRead throws). The finally block must still clean up.
        var (scenario, ownerSong) = CreateSharedSongScenario();
        // Wipe the owner's actual audio file so staging cannot copy it
        File.Delete(ownerSong.RepositoryPath);

        var tempBefore = Directory.EnumerateDirectories(Path.GetTempPath(), "mymusic_shared_import_*").ToList();

        // Act — the import attempts to stage the bytes and fails (file not found)
        await scenario.ImportService.ImportAsync(
            scenario.DbContext, ownerSong.Id, scenario.Recipient.Id, CancellationToken.None);

        // Assert — even on failure the temp staging dir is gone (finally block)
        var tempAfter = Directory.EnumerateDirectories(Path.GetTempPath(), "mymusic_shared_import_*").ToList();
        var leftover = tempAfter.Except(tempBefore).ToList();
        leftover.ShouldBeEmpty("temp import directory must be cleaned up on failure");
    }
}