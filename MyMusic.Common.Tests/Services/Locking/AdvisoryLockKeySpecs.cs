using MyMusic.Common.Services;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Locking;

public class AdvisoryLockKeySpecs
{
    [Fact]
    public void Create_SameInputs_ReturnsSameKey()
    {
        var first = AdvisoryLockKey.Create(AdvisoryLockScope.Artist, 1, "Amaranthe");
        var second = AdvisoryLockKey.Create(AdvisoryLockScope.Artist, 1, "Amaranthe");

        second.ShouldBe(first);
    }

    [Fact]
    public void Create_DifferentOwners_ReturnsDifferentKeys()
    {
        // Locks are per user, so imports of different users never contend
        var first = AdvisoryLockKey.Create(AdvisoryLockScope.Artist, 1, "Amaranthe");
        var second = AdvisoryLockKey.Create(AdvisoryLockScope.Artist, 2, "Amaranthe");

        second.ShouldNotBe(first);
    }

    [Fact]
    public void Create_DifferentScopes_ReturnsDifferentClassIds()
    {
        // An artist and an album with the same name are different resources
        var artist = AdvisoryLockKey.Create(AdvisoryLockScope.Artist, 1, "The Nexus");
        var album = AdvisoryLockKey.Create(AdvisoryLockScope.Album, 1, "The Nexus");

        album.ClassId.ShouldNotBe(artist.ClassId);
    }

    [Fact]
    public void Create_SameConcatenatedParts_ReturnsDifferentKeys()
    {
        // Album "Burn With Me" by "A" is not album "With Me" by "A Burn"
        var first = AdvisoryLockKey.Create(AdvisoryLockScope.Album, 1, "A", "Burn With Me");
        var second = AdvisoryLockKey.Create(AdvisoryLockScope.Album, 1, "A Burn", " With Me");

        second.ShouldNotBe(first);
    }

    [Fact]
    public void Normalize_RemovesDuplicatesAndSortsByClassThenObject()
    {
        var album = AdvisoryLockKey.Create(AdvisoryLockScope.Album, 1, "Artist", "Album");
        var artistB = new AdvisoryLockKey(album.ClassId - 1, 20);
        var artistA = new AdvisoryLockKey(album.ClassId - 1, -5);

        var normalized = AdvisoryLockKey.Normalize([album, artistB, artistA, album, artistB]);

        // Every caller acquires in this same order, so no two callers can wait on each other in a cycle
        normalized.ShouldBe([artistA, artistB, album]);
    }
}
