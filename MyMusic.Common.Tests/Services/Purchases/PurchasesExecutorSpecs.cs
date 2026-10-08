using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Services.Sync;
using MyMusic.Common.Sources;
using MyMusic.Common.Targets;
using MyMusic.Common.Tests.Services.BackgroundJobs;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Purchases;

public class PurchasesExecutorSpecs
{
    private readonly Scenario _scenario = new();
    private readonly ISourcesService _sourcesService = Substitute.For<ISourcesService>();
    private readonly ISource _sourceClient = Substitute.For<ISource>();
    private readonly Source _source;
    private readonly Album _album;
    private readonly Device _device;

    public PurchasesExecutorSpecs()
    {
        _source = _scenario.CreateSource();
        _album = _scenario.CreateAlbum("Album", _scenario.CreateArtist("Artist"));
        _device = _scenario.CreateDevice("Phone");
        _sourcesService.GetSourceClientAsync(_source.Id, Arg.Any<CancellationToken>()).Returns(_sourceClient);
    }

    private PurchasesQueue.PurchasesExecutor CreateExecutor(ISongFileReplaceService? songFileReplace = null) =>
        new(_scenario.DbContext, _scenario.CreateMusicService(), _sourcesService, _scenario.FileSystem,
            songFileReplace ?? new SongFileReplaceService(_scenario.DbContext, _scenario.FileSystem,
                _scenario.AdvisoryLocks, _scenario.FileTransactions, _scenario.CreateSongFileUpdateService(),
                new SyncPathResolver(), Substitute.For<ILogger<SongFileReplaceService>>()),
            new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>()));

    /// <summary>
    /// Makes the source sell a file that carries another song's tags, around audio that differs from every other
    /// file's.
    /// </summary>
    private void SellFileOfAnotherSong(PurchasedSong purchase)
    {
        const string path = "/source/sold.mp3";
        MockMusicFile.CreateWithDifferentContent(_scenario.FileSystem, path, "Other Title", "Other Album",
            ["Other Artist"], ["Pop"], year: 1999);

        _sourceClient.PurchaseSongAsync(purchase.ExternalId, Arg.Any<CancellationToken>())
            .Returns(_ => new MemoryStream(_scenario.FileSystem.File.ReadAllBytes(path)));
    }

    [Fact]
    public async Task ExecuteAsync_PurchaseReplacingSongFile_ReplacesAudioAndKeepsTheSongMetadata()
    {
        // Arrange
        var song = await _scenario.CreateSyncedSongAsync("Song", _album, _device);
        var importDevice = _scenario.CreateDevice("Auto import");
        importDevice.ImportOnPurchase = true;
        _scenario.DbContext.SaveChanges();
        var purchase = _scenario.CreatePurchase(_source, _scenario.AdminUser.Id, PurchasedSongStatus.Acquiring,
            replacesFileOf: song);
        SellFileOfAnotherSong(purchase);

        // Act
        await CreateExecutor().ExecuteAsync(purchase, CancellationToken.None);

        // Assert
        _scenario.DbContext.ChangeTracker.Clear();
        _scenario.DbContext.Songs.Count().ShouldBe(1);

        var reloaded = _scenario.LoadSong(song.Id);
        reloaded.Title.ShouldBe("Song");
        reloaded.Album.Name.ShouldBe("Album");
        reloaded.Artists.Select(sa => sa.Artist.Name).ShouldBe(["Artist"]);
        reloaded.Checksum.ShouldNotBe(song.Checksum);

        using (var file = TagLib.File.Create(
                   new FileSystemFileAbstraction(_scenario.FileSystem.FileInfo.New(reloaded.RepositoryPath))))
        {
            file.Tag.Title.ShouldBe("Song");
            file.Tag.Album.ShouldBe("Album");
        }

        // Only the devices the song was already in get the new file
        var songDevice = reloaded.Devices.ShouldHaveSingleItem();
        songDevice.DeviceId.ShouldBe(_device.Id);
        songDevice.SyncAction.ShouldBe(SongSyncAction.Download);

        _scenario.DbContext.PurchasedSongs.Single().SongId.ShouldBe(song.Id);
        await _sourceClient.DidNotReceive().GetSongAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PurchaseReplacingSongFile_ReplacementFails_ThrowsAndLeavesSongUntouched()
    {
        // Arrange
        var song = await _scenario.CreateSyncedSongAsync("Song", _album, _device);
        var filesBefore = _scenario.ReadMusicFiles();
        var purchase = _scenario.CreatePurchase(_source, _scenario.AdminUser.Id, PurchasedSongStatus.Acquiring,
            replacesFileOf: song);
        _sourceClient.PurchaseSongAsync(purchase.ExternalId, Arg.Any<CancellationToken>())
            .Returns(_ => new MemoryStream("This is not an audio file"u8.ToArray()));

        // Act & Assert
        await Should.ThrowAsync<ValidationException>(() =>
            CreateExecutor().ExecuteAsync(purchase, CancellationToken.None));
        _scenario.ShouldHaveUnchangedSongs([song], filesBefore);
    }

    [Fact]
    public async Task ExecuteAsync_PurchaseReplacingSongFile_SourceFails_ThrowsWithoutReplacing()
    {
        // Arrange
        var song = await _scenario.CreateSyncedSongAsync("Song", _album, _device);
        var purchase = _scenario.CreatePurchase(_source, _scenario.AdminUser.Id, PurchasedSongStatus.Acquiring,
            replacesFileOf: song);
        var songFileReplace = Substitute.For<ISongFileReplaceService>();
        _sourceClient.PurchaseSongAsync(purchase.ExternalId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Source unavailable"));

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(() =>
            CreateExecutor(songFileReplace).ExecuteAsync(purchase, CancellationToken.None));
        await songFileReplace.DidNotReceiveWithAnyArgs().ReplaceAsync(default, default, default!);
    }
}
