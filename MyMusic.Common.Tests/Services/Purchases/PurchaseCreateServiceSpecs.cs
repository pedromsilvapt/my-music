using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Purchases;
using MyMusic.Common.Sources;
using MyMusic.Common.Tests.Services.BackgroundJobs;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Purchases;

public class PurchaseCreateServiceSpecs
{
    private const string ExternalId = "ext-1";

    private readonly Scenario _scenario = new();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISourcesService _sourcesService = Substitute.For<ISourcesService>();
    private readonly Source _source;

    public PurchaseCreateServiceSpecs()
    {
        _currentUser.Id.Returns(_scenario.AdminUser.Id);
        _source = _scenario.CreateSource();

        var artist = new SourceSongArtist { Id = "a1", Name = "Source Artist" };
        var client = Substitute.For<ISource>();
        client.GetSongAsync(ExternalId, Arg.Any<CancellationToken>()).Returns(new SourceSong
        {
            Id = ExternalId,
            Title = "Source Title",
            Album = new SourceSongAlbum { Id = "al1", Name = "Source Album", Artist = artist },
            Artists = [artist],
            Genres = [],
            Year = 2001,
        });
        _sourcesService.GetSourceClientAsync(_source.Id, Arg.Any<CancellationToken>()).Returns(client);
    }

    private PurchaseCreateService CreateService() => new(_scenario.DbContext, _currentUser, _sourcesService);

    [Fact]
    public async Task CreateAsync_NoSongToReplace_QueuesPurchaseOfANewSong()
    {
        // Act
        var purchase = await CreateService().CreateAsync(_source.Id, ExternalId);

        // Assert
        purchase.ShouldNotBeNull();
        var stored = _scenario.DbContext.PurchasedSongs.ShouldHaveSingleItem();
        stored.Id.ShouldBe(purchase.Id);
        stored.UserId.ShouldBe(_scenario.AdminUser.Id);
        stored.SourceId.ShouldBe(_source.Id);
        stored.ExternalId.ShouldBe(ExternalId);
        stored.Title.ShouldBe("Source Title");
        stored.SubTitle.ShouldBe("Source Artist • Source Album • 2001");
        stored.Status.ShouldBe(PurchasedSongStatus.Queued);
        stored.Progress.ShouldBe(0);
        stored.SongId.ShouldBeNull();
        stored.ReplacesSongFile.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_SongToReplace_QueuesPurchaseTargetingTheSong()
    {
        // Arrange
        var song = _scenario.CreateSong("Song");

        // Act
        var purchase = await CreateService().CreateAsync(_source.Id, ExternalId, song.Id);

        // Assert
        purchase.ShouldNotBeNull();
        var stored = _scenario.DbContext.PurchasedSongs.ShouldHaveSingleItem();
        stored.SongId.ShouldBe(song.Id);
        stored.ReplacesSongFile.ShouldBeTrue();
        stored.Status.ShouldBe(PurchasedSongStatus.Queued);
        // The purchase is described by the song of the source, not by the one it replaces the audio of
        stored.Title.ShouldBe("Source Title");
    }

    [Fact]
    public async Task CreateAsync_SongToReplaceOfAnotherUser_ReturnsNullAndQueuesNothing()
    {
        // Arrange
        var song = _scenario.CreateSong("Song");
        _currentUser.Id.Returns(_scenario.CreateUser("Other", "other").Id);

        // Act
        var purchase = await CreateService().CreateAsync(_source.Id, ExternalId, song.Id);

        // Assert
        purchase.ShouldBeNull();
        _scenario.DbContext.PurchasedSongs.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_UnknownSongToReplace_ReturnsNullAndQueuesNothing()
    {
        // Act
        var purchase = await CreateService().CreateAsync(_source.Id, ExternalId, 123456);

        // Assert
        purchase.ShouldBeNull();
        _scenario.DbContext.PurchasedSongs.ShouldBeEmpty();
    }
}
