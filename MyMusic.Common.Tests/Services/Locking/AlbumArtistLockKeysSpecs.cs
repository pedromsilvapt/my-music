using MyMusic.Common.Services;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Locking;

public class AlbumArtistLockKeysSpecs
{
    [Fact]
    public void Create_ReturnsArtistKeysAlbumKeysAndTheKeysOfEveryAlbumArtist()
    {
        // Act
        var keys = AlbumArtistLockKeys.Create(7, ["Artist A", "Artist A", "Artist B"], [("Artist C", "Album")]);

        // Assert
        keys.ShouldBe(
        [
            AdvisoryLockKey.Create(AdvisoryLockScope.Artist, 7, "Artist A"),
            AdvisoryLockKey.Create(AdvisoryLockScope.Artist, 7, "Artist B"),
            AdvisoryLockKey.Create(AdvisoryLockScope.Artist, 7, "Artist C"),
            AdvisoryLockKey.Create(AdvisoryLockScope.Album, 7, "Artist C", "Album"),
        ], ignoreOrder: true);
    }
}
